/*
 *
 *	Adventure Creator
 *	by Chris Burton, 2013-2024
 *	
 *	"ActionConversation.cs"
 * 
 *	This action turns on a conversation.
 * 
 */

 using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GGJ2025
{

	[System.Serializable]
	public class ActionConversationFromFile : AC.Action
	{

		public int parameterID = -1;
		public int constantID = 0;
		public AC.Conversation conversation;
		protected AC.Conversation runtimeConversation;

		[SerializeField] private List<int> overrideOptionSocketIDs = new List<int> ();

		public bool overrideOptions = false;
		protected AC.ActionList parentActionList;
		#if UNITY_EDITOR
		protected AC.Conversation tempConversation;
		#endif

		public bool setElement;
		public string menuName;
		public string containerElementName;

		protected AC.LocalVariables localVariables;
		protected AC.Menu runtimeMenu;
		protected AC.MenuDialogList runtimeDialogList;

		public int menuParameterID = -1;
		public int elementParameterID = -1;

		public int numSockets;


		public override AC.ActionCategory Category { get { return AC.ActionCategory.Dialogue; }}
		public override string Title { get { return "Start conversation"; }}
		public override string Description { get { return "Enters Conversation mode, and displays the available dialogue options in a specified conversation."; }}
		public override int NumSockets { get { return numSockets; }}


		public override void AssignParentList (AC.ActionList actionList)
		{
			parentActionList = actionList;

			if (localVariables == null)
			{
				localVariables = AC.KickStarter.localVariables;
			}

			base.AssignParentList (actionList);
		}


		public override void AssignValues (List<AC.ActionParameter> parameters)
		{
			runtimeConversation = AssignFile <AC.Conversation> (parameters, parameterID, constantID, conversation);

			if (!overrideOptions && setElement)
			{
					string runtimeMenuName = AssignString (parameters, menuParameterID, menuName);
				string runtimeContainerElementName = AssignString (parameters, elementParameterID, containerElementName);

				runtimeMenuName = AC.AdvGame.ConvertTokens (runtimeMenuName, AC.Options.GetLanguage (), localVariables, parameters);
				runtimeContainerElementName = AC.AdvGame.ConvertTokens (runtimeContainerElementName, AC.Options.GetLanguage (), localVariables, parameters);

				runtimeMenu = AC.PlayerMenus.GetMenuWithName (runtimeMenuName);
				if (runtimeMenu)
				{
					AC.MenuElement element = runtimeMenu.GetElementWithName (runtimeContainerElementName);
					if (element)
					{
						runtimeDialogList = element as AC.MenuDialogList;
					}
				}
				if (runtimeDialogList == null)
				{
					LogWarning ("Cannot find DialogList element " + runtimeContainerElementName + " inside Menu " + runtimeMenuName);
				}
			}

			if (overrideOptions && parameterID < 0 && runtimeConversation)
			{
				UpdateSocketIDs(runtimeConversation);
			}
		}

		
		public override float Run ()
		{
			if (runtimeConversation == null)
			{
				LogWarning ("Cannot start conversation - no Conversation assigned");
				return 0f;
			}

			isRunning = false;

			if (overrideOptions)
			{
				if (runtimeConversation.lastOption >= 0)
				{
					AC.KickStarter.actionListManager.ignoreNextConversationSkip = true;
					return 0f;
				}
				AC.KickStarter.actionListManager.ignoreNextConversationSkip = false;
			}

			if (!overrideOptions && setElement && runtimeDialogList)
			{
				runtimeDialogList.OverrideConversation = runtimeConversation;
				runtimeMenu.TurnOn ();
			}

			AC.ActionConversation actionConversation = new AC.ActionConversation
			{
				conversation = runtimeConversation,
				overrideOptions = overrideOptions,
				menuName = menuName,
				containerElementName = containerElementName,
				menuParameterID = menuParameterID,
				elementParameterID = elementParameterID
			};

			runtimeConversation.Interact (parentActionList, actionConversation);
			
			return 0f;
		}


		public override void Skip ()
		{
			if (AC.KickStarter.actionListManager.ignoreNextConversationSkip)
			{
				AC.KickStarter.actionListManager.ignoreNextConversationSkip = false;
				return;
			}

			Run ();
		}

		
		public override int GetNextOutputIndex ()
		{
			if (runtimeConversation)
			{
				int _chosenOptionIndex = runtimeConversation.lastOption;
				
				runtimeConversation.lastOption = -1;
				if (overrideOptions && _chosenOptionIndex >= 0 && endings.Count > _chosenOptionIndex)
				{
					return _chosenOptionIndex;
				}
			}
			
			return -1;
		}


		#if UNITY_EDITOR

		public override void ShowGUI (List<AC.ActionParameter> parameters)
		{
			ComponentField ("Conversation:", ref conversation, ref constantID, parameters, ref parameterID);

			if (conversation)
			{
				overrideOptions = EditorGUILayout.Toggle ("Override options?", overrideOptions);

				if (overrideOptions)
				{
					UpdateSocketIDs (conversation);
					numSockets = conversation.options.Count;
				}
				else
				{
					numSockets = 0;
				}
			}
			else if (parameterID >= 0)
			{
				overrideOptions = EditorGUILayout.Toggle ("Override options?", overrideOptions);

				if (overrideOptions)
				{
					tempConversation = (AC.Conversation) EditorGUILayout.ObjectField ("Placeholder conv:", tempConversation, typeof (ConversationFromFile), true);
					if (tempConversation != null)
					{
						numSockets = tempConversation.options.Count;
					}
					else
					{
						EditorGUILayout.HelpBox ("To set override options when the Conversation is parameterised, a placeholder Conversation must be assigned.", MessageType.Info);
					}
				}
				else
				{
					numSockets = 0;
				}
			}
			else
			{
				if (isAssetFile && overrideOptions && constantID != 0)
				{
					EditorGUILayout.HelpBox ("Cannot find linked Conversation - please open its scene file.", MessageType.Warning);
				}
				else
				{
					numSockets = 0;
				}
			}

			if (!overrideOptions)
			{
				setElement = EditorGUILayout.Toggle ("Open in set element?", setElement);
				if (setElement)
				{
					TextField ("Menu name:", ref menuName, parameters, ref menuParameterID);
					TextField ("DialogList name:", ref containerElementName, parameters, ref elementParameterID);
				}
			}

			if (!overrideOptions && !AC.KickStarter.settingsManager.allowGameplayDuringConversations)
			{
				willWait = EditorGUILayout.Toggle ("Wait until finish?", willWait);
				if (willWait)
				{
					numSockets = 1;
				}
			}
		}


		protected override string GetSocketLabel (int i)
		{
			if (!overrideOptions && !AC.KickStarter.settingsManager.allowGameplayDuringConversations && willWait)
			{
				return "After running:";
			}

			if (parameterID >= 0 && tempConversation != null && tempConversation.options.Count > i)
			{
				return ("'" + tempConversation.options[i].label + "':");
			}

			if (conversation != null && conversation.options.Count > i)
			{
				return ("'" + conversation.options[i].label + "':");
			}
			return "Option " + i.ToString () + ":";
		}


		public override void AssignConstantIDs (bool saveScriptsToo, bool fromAssetFile)
		{
			if (saveScriptsToo)
			{
				AddSaveScript <AC.RememberConversation> (conversation);
			}
			constantID = AssignConstantID<AC.Conversation> (conversation, constantID, parameterID);
		}

		
		public override string SetLabel ()
		{
			if (conversation != null)
			{
				return conversation.name;
			}
			return string.Empty;
		}


		public override bool ReferencesObjectOrID (GameObject _gameObject, int id)
		{
			if (parameterID < 0)
			{
				if (conversation && conversation.gameObject == _gameObject) return true;
				if (constantID == id) return true;
			}
			return base.ReferencesObjectOrID (_gameObject, id);
		}

		#endif
		

		private void UpdateSocketIDs (AC.Conversation _conversation)
		{
			List<int> newOverrideOptionSocketIDs = new List<int> ();
			for (int i = 0; i < _conversation.options.Count; i++)
			{
				int newOptionID = _conversation.options[i].ID;
				if (!newOverrideOptionSocketIDs.Contains (newOptionID))
				{
					newOverrideOptionSocketIDs.Add (newOptionID);
				}
			}

			if (overrideOptionSocketIDs == null || overrideOptionSocketIDs.Count == 0)
			{
				overrideOptionSocketIDs = newOverrideOptionSocketIDs;
			}
			else
			{
				// Deleted options since last check?
				for (int i = 0; i < overrideOptionSocketIDs.Count; i++)
				{
					if (!newOverrideOptionSocketIDs.Contains (overrideOptionSocketIDs[i]))
					{
						overrideOptionSocketIDs.RemoveAt (i);
						endings.RemoveAt (i);
						i = -1;
					}
				}

				// Added options since last check?
				for (int i = 0; i < newOverrideOptionSocketIDs.Count; i++)
				{
					if (!overrideOptionSocketIDs.Contains (newOverrideOptionSocketIDs[i]))
					{
						overrideOptionSocketIDs.Add (newOverrideOptionSocketIDs[i]);
						endings.Add (new AC.ActionEnd (true));
						i = -1;
					}
				}

				// Now lists should contain same IDs (but order may differ)
				for (int i = 0; i < newOverrideOptionSocketIDs.Count; i++)
				{
					int newOptionID = newOverrideOptionSocketIDs[i];
					if (overrideOptionSocketIDs[i] != newOptionID)
					{
						int oldIndex = overrideOptionSocketIDs.IndexOf (newOptionID);
						if (oldIndex > i)
						{
							overrideOptionSocketIDs.RemoveAt (oldIndex);
							overrideOptionSocketIDs.Insert (i, newOptionID);
							AC.ActionEnd oldEnding = new AC.ActionEnd (endings[oldIndex]);
							endings.RemoveAt (oldIndex);
							endings.Insert (i, oldEnding);
							i = -1;
						}
					}
				}
			}
		}


		/**
		 * <summary>Creates a new instance of the 'Dialogue: Start conversation' Action</summary>
		 * <param name = "conversationToRun">The Conversation to begin</param>
		 * <returns>The generated Action</returns>
		 */
		public static ActionConversationFromFile CreateNew (GGJ2025.ConversationFromFile conversationToRun)
		{
			ActionConversationFromFile newAction = CreateNew<ActionConversationFromFile> ();
			newAction.conversation = conversationToRun;
			newAction.TryAssignConstantID (newAction.conversation, ref newAction.constantID);
			return newAction;
		}

	}

}