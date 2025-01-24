/*
 *
 *	Adventure Creator
 *	by Chris Burton, 2013-2024
 *	
 *	"ConversationFromFile.cs"
 * 
 *	This script is handles character conversations.
 *	It generates instances of DialogOption for each line
 *	that the player can choose to say.
 * 
 */

using UnityEngine;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.Serialization;
using System.Linq;
using System;
using UnityEngine.UI;
using Unity.VisualScripting;

namespace GGJ2025
{    
	[Serializable]
	public enum ConversationStage
	{
		Question,
		Response,
		Action
	}
	
	[Serializable]
	public class ButtonDialogExtended : AC.ButtonDialog 
	{
		public object customData;
		
		public ButtonDialogExtended(int[] idArray) : base(idArray) { }
	}

	// Move interface declaration to namespace level
	public interface IConversationSystem
	{
		void ShowOptions(IEnumerable<ButtonDialogExtended> options);
		void HideOptions();
		void RefreshUI();
	}

	// Default implementation
	public class AdventureCreatorConversationSystem : IConversationSystem
	{
		public void ShowOptions(IEnumerable<ButtonDialogExtended> options)
		{
			if (AC.KickStarter.playerMenus != null)
			{
				AC.KickStarter.playerMenus.RefreshDialogueOptions();
			}
		}

		public void HideOptions()
		{
			if (AC.KickStarter.playerInput != null)
			{
				AC.KickStarter.playerInput.EndConversation();
			}
		}

		public void RefreshUI()
		{
			if (AC.KickStarter.playerMenus != null)
			{
				AC.KickStarter.playerMenus.RefreshDialogueOptions();
			}
		}
	}

	/**
	 * This component provides the player with a list of dialogue options that their character can say.
	 * Options are display in a MenuDialogList element, and will usually run a DialogueOption ActionList when clicked - unless overrided by the "Dialogue: Start conversation" Action that triggers it.
	 */
	[AddComponentMenu("Adventure Creator/Logic/Conversation From File")]
	[HelpURL("https://www.adventurecreator.org/scripting-guide/class_a_c_1_1_conversation.html")]
	public class ConversationFromFile : AC.Conversation
	{
		private IConversationSystem conversationSystem;

		private void Awake()
		{
			AC.ACDebug.Log("ConversationFromFile Awake called", this);
			conversationSystem = GetComponent<IConversationSystem>() ?? 
				new AdventureCreatorConversationSystem();
			
			// Hook into AC's event system
			AC.EventManager.OnClickConversation += OnClickConversation;
		}

		private void OnDestroy()
		{
			AC.EventManager.OnClickConversation -= OnClickConversation;
		}

		private void OnClickConversation(AC.Conversation conversation, int optionID)
		{
			if (conversation == this && loadFromJson && jsonFile != null)
			{
				AC.ACDebug.Log($"Intercepting conversation click with optionID: {optionID}", this);
				
				// Only load new options if we don't have any or if this is a new conversation
				if (options.Count == 0 || AC.KickStarter.playerInput.activeConversation != this)
				{
					LoadConversationFromJson();
				}

				// Verify the option exists before running it
				if (GetOptionWithID(optionID) != null)
				{
					RunOption(optionID);
					AC.KickStarter.playerMenus?.RefreshDialogueOptions();
				}
				else
				{
					AC.ACDebug.LogWarning($"Invalid option ID: {optionID}", this);
				}
			}
		}

		#region Variables

		[Header("JSON Configuration")]
		[SerializeField] private TextAsset jsonFile;
		[Tooltip("If true, will load conversation options from the JSON file when enabled")]
		[SerializeField] private bool loadFromJson = false;
		[SerializeField] private ConversationStage stage = ConversationStage.Question;

		private ConversationData jsonData;
		private int lastGeneratedId = 0;  // Track the last generated ID

		private readonly Dictionary<ConversationStage, Action<ConversationOptionData>> stageHandlers;

		#endregion

		#region UnityStandards

		private void OnEnable ()
		{
			AC.EventManager.OnEndActionList += OnEndActionList;
			AC.EventManager.OnEndConversation += OnEndConversation;
			AC.EventManager.OnFinishLoading += OnFinishLoading;

			// Add JSON loading logic
			if (loadFromJson && jsonFile != null)
			{
				LoadConversationFromJson();
			}
		}


		private void OnDisable ()
		{
			AC.EventManager.OnEndActionList -= OnEndActionList;
			AC.EventManager.OnEndConversation -= OnEndConversation;
			AC.EventManager.OnFinishLoading -= OnFinishLoading;
		}

		private void Start ()
		{
			AC.ACDebug.Log("ConversationFromFile Start called", this);
			if (AC.KickStarter.inventoryManager)
			{
				foreach (AC.ButtonDialog option in options)
				{
					if (option.linkToInventory && option.cursorIcon.texture == null)
					{
						AC.InvItem linkedItem = AC.KickStarter.inventoryManager.GetItem (option.linkedInventoryID);
						if (linkedItem != null && linkedItem.tex != null)
						{
							option.cursorIcon.ReplaceTexture (linkedItem.tex);
						}
					}
				}
			}
		}

		#endregion


		#region PublicFunctions

		/**
		 * <summary>Checks if the Conversation is currently active.</summary>
		 * <param name = "includeResponses">If True, then the Conversation will be considered active if any of its dialogue option ActionLists are currently-running, as opposed to only when its options are displayed as choices on screen</param>
		 * </returns>True if the Conversation is active</returns>
		 */
		public new bool IsActive (bool includeResponses)
		{
			AC.ACDebug.Log($"[ConversationFromFile] IsActive called with includeResponses: {includeResponses}", this);
			if (AC.KickStarter.playerInput.activeConversation == this ||
				AC.KickStarter.playerInput.PendingOptionConversation == this)
			{
				return true;
			}

			if (includeResponses)
			{
				foreach (AC.ButtonDialog buttonDialog in options)
				{
					if (AC.KickStarter.actionListManager.IsListRunning (buttonDialog.dialogueOption))
					{
						return true;
					}
				}
			}
			return false;
		}


		/** Hides the Conversation's dialogue options, if it is the currently-active Conversation. */
		public new void TurnOff ()
		{
			AC.ACDebug.Log("[ConversationFromFile] TurnOff called", this);
			if (AC.KickStarter.playerInput && AC.KickStarter.playerInput.activeConversation == this)
			{
				CancelInvoke ("RunDefault");
				AC.KickStarter.playerInput.EndConversation ();
			}
		}


		/**
		 * <summary>Runs a dialogue option.</summary>
		 * <param name = "slot">The index number of the dialogue option to run</param>
		 * <param name = "force">If True, then the option will be run regardless of whether it's enabled or valid</param>
		 */
		public new void RunOption(int slot, bool force = false)
		{
			AC.ACDebug.Log($"[ConversationFromFile] RunOption called with slot: {slot}, force: {force}", this);
			int i = ConvertSlotToOption(slot, force);
			if (i == -1 || i >= options.Count)
			{
				AC.ACDebug.LogWarning($"Invalid slot {slot} or option index {i}", this);
				return;
			}

			CancelInvoke("RunDefault");
			
			AC.ButtonDialog buttonDialog = options[i];
			if (force || options.Contains(buttonDialog))
			{
				if (buttonDialog is ButtonDialogExtended extendedDialog)
				{
					AC.ACDebug.Log($"Running extended dialog option with ID: {extendedDialog.ID}", this);
					RunOption(extendedDialog);
				}
				else
				{
					AC.ACDebug.Log($"Running base dialog option with ID: {buttonDialog.ID}", this);
					base.RunOption(buttonDialog);
				}
			}
		}


		/**
		 * <summary>Runs a dialogue option with a specific ID.</summary>
		 * <param name = "ID">The ID number of the dialogue option to run</param>
		 * <param name = "force">If True, then the option will be run regardless of whether it's enabled or valid</param>
		 */
		public new void RunOptionWithID (int ID, bool force = false)
		{
			AC.ACDebug.Log($"[ConversationFromFile] RunOptionWithID called with ID: {ID}, force: {force}", this);
			AC.ButtonDialog buttonDialog = GetOptionWithID (ID);
			if (buttonDialog == null) return;

			if (!buttonDialog.isOn && !force) return;

			CancelInvoke ("RunDefault");

			if (!gameObject.activeInHierarchy || force || interactionSource == AC.InteractionSource.CustomScript)
			{
				RunOption (buttonDialog);
			}
			else
		
			{
				AC.KickStarter.playerInput.StartCoroutine (AC.KickStarter.playerInput.DelayConversation (this, () => RunOption (buttonDialog)));
			}

			AC.KickStarter.playerInput.activeConversation = null;

			if (overrideActiveList != null)
			{
				AC.KickStarter.eventManager.Call_OnEndConversation (this);
			}
		}


		/**
		 * <summary>Gets the time remaining before a timed Conversation ends.</summary>
		 * <returns>The time remaining before a timed Conversation ends.</returns>
		 */
		public new float GetTimeRemaining ()
		{
			return ((startTime + timer - Time.time) / timer);
		}


		/**
		 * <summary>Checks if a given slot exists</summary>
		 * <param name = "slot">The index number of the enabled dialogue option to find</param>
		 * <returns>True if a given slot exists</returns>
		 */
		public new bool SlotIsAvailable (int slot)
		{
			int i = ConvertSlotToOption (slot);
			return (i >= 0 && i < options.Count);
		}


		/**
		 * <summary>Gets the ID of a dialogue option.</summary>
		 * <param name = "slot">The index number of the enabled dialogue option to find</param>
		 * <returns>The dialogue option's ID number, if found - or -1 otherwise.</returns>
		 */
		public new int GetOptionID (int slot)
		{
			int i = ConvertSlotToOption (slot);
			if (i >= 0 && i < options.Count)
			{
				return options[i].ID;
			}
			return -1;
		}


		/**
		 * <summary>Gets the display label of a dialogue option.</summary>
		 * <param name = "slot">The index number of the enabled dialogue option to find</param>
		 * <returns>The display label of the dialogue option</returns>
		 */
		public new string GetOptionName (int slot)
		{
			int i = ConvertSlotToOption (slot);
			if (i == -1)
			{
				i = 0;
			}

			string translatedLine = AC.KickStarter.runtimeLanguages.GetTranslation (options[i].label, options[i].lineID, AC.Options.GetLanguage (), GetTranslationType (0));
			return AC.AdvGame.ConvertTokens (translatedLine).Replace ("\\n", "\n");
		}


		/**
		 * <summary>Gets the display label of a dialogue option with a specific ID.</summary>
		 * <param name = "ID">The ID of the dialogue option to find</param>
		 * <returns>The display label of the dialogue option</returns>
		 */
		public new string GetOptionNameWithID (int ID)
		{
			AC.ButtonDialog buttonDialog = GetOptionWithID (ID);
			if (buttonDialog == null) return null;

			string translatedLine = AC.KickStarter.runtimeLanguages.GetTranslation (buttonDialog.label, buttonDialog.lineID, AC.Options.GetLanguage (), GetTranslationType (0));
			return AC.AdvGame.ConvertTokens (translatedLine).Replace ("\\n", "\n");
		}


		/**
		 * <summary>Gets the display icon of a dialogue option.</summary>
		 * <param name = "slot">The index number of the dialogue option to find</param>
		 * <returns>The display icon of the dialogue option</returns>
		 */
		public new AC.CursorIconBase GetOptionIcon (int slot)
		{
			int i = ConvertSlotToOption (slot);
			if (i == -1)
			{
				i = 0;
			}
			return options[i].cursorIcon;
		}


		/**
		 * <summary>Gets the display icon of a dialogue option with a specific ID.</summary>
		 * <param name = "ID">The ID of the dialogue option to find</param>
		 * <returns>The display icon of the dialogue option</returns>
		 */
		public new AC.CursorIconBase GetOptionIconWithID (int ID)
		{
			AC.ButtonDialog buttonDialog = GetOptionWithID (ID);
			if (buttonDialog == null) return null;
			return buttonDialog.cursorIcon;
		}


		/**
		 * <summary>Gets the ButtonDialog data container, which stores data for a dialogue option.</summary>
		 * <param name = "slot">The index number of the dialogue option to find</param>
		 * <returns>The ButtonDialog data container</returns>
		 */
		public new  AC.ButtonDialog GetOption (int slot)
		{
			int i = ConvertSlotToOption (slot);
			if (i == -1)
			{
				i = 0;
			}
			return options[i];
		}


		/**
		 * <summary>Gets the ButtonDialog data container with a given ID number, which stores data for a dialogue option.</summary>
		 * <param name = "id">The ID number associated with the dialogue option to find</param>
		 * <returns>The ButtonDialog data container</returns>
		 */
		public new AC.ButtonDialog GetOptionWithID (int id)
		{
			for (int i=0; i<options.Count; i++)
			{
				if (options[i].ID == id)
				{
					return options[i];
				}
			}
			return null;
		}


		/**
		 * <summary>Gets the number of dialogue options that are currently enabled.</summary>
		 * <returns>The number of currently-enabled dialogue options</returns>
		 */
		public new int GetNumEnabledOptions ()
		{
			int num = 0;
			for (int i=0; i<options.Count; i++)
			{
				if (options[i].isOn)
				{
					num++;
				}
			}
			return num;
		}


		/**
		 * <summary>Checks if a dialogue option has been chosen at least once by the player.</summary>
		 * <param name = "slot">The index number of the dialogue option to find</param>
		 * <returns>True if the dialogue option has been chosen at least once by the player.</returns>
		 */
		public new bool OptionHasBeenChosen (int slot)
		{
			int i = ConvertSlotToOption (slot);
			if (i == -1)
			{
				i = 0;
			}
			return options[i].hasBeenChosen;
		}


		/**
		 * <summary>Un-marks a specific dialogue option as having been chosen by the player.</summary>
		 * <param name="ID">The ID of the dialogue option</param>
		 */
		public new void UnmarkAsChosen (int ID)
		{
			AC.ButtonDialog buttonDialog = GetOptionWithID (ID);
			if (buttonDialog == null) return;
			buttonDialog.hasBeenChosen = false;
		}


		/** Un-marks all dialogue options as having been chosen by the player. */
		public new void UnmarkAllAsChosen ()
		{
			foreach (AC.ButtonDialog buttonDialog in options)
			{
				buttonDialog.hasBeenChosen = false;
			}
		}


		/**
		 * <summary>Checks if a dialogue option with a specific ID has been chosen at least once by the player.</summary>
		 * <param name = "ID">The ID of the dialogue option to find</param>
		 * <returns>True if the dialogue option has been chosen at least once by the player.</returns>
		 */
		public new bool OptionWithIDHasBeenChosen (int ID)
		{
			AC.ButtonDialog buttonDialog = GetOptionWithID (ID);
			if (buttonDialog == null) return false;
			return buttonDialog.hasBeenChosen;
		}


		/** 
		 * <summary>Checks if all options have been chosen at least once by the player</summary>
		 * <param name = "onlyEnabled">If True, then only options that are currently enabled will be included in the check</param>
		 * <returns>True if all options have been chosen at least once by the player</returns>
		 */
		public new bool AllOptionsBeenChosen (bool onlyEnabled)
		{
			foreach (AC.ButtonDialog option in options)
			{
				if (!option.hasBeenChosen)
				{
					if (onlyEnabled && !option.isOn)
					{
						continue;
					}
					return false;
				}
			}
			return true;
		}


		/**
		 * <summary>Turns a dialogue option on, provided that it is unlocked.</summary>
		 * <param name = "id">The ID number of the dialogue option to enable</param>
		 */
		public new void TurnOptionOn (int id)
		{
			foreach (AC.ButtonDialog option in options)
			{
				if (option.ID == id)
				{
					if (!option.isLocked)
					{
						option.isOn = true;
					}
					else
					{
						AC.ACDebug.Log (gameObject.name + "'s option '" + option.label + "' cannot be turned on as it is locked.", this);
					}
					return;
				}
			}
		}


		/**
		 * <summary>Turns a dialogue option off, provided that it is unlocked.</summary>
		 * <param name = "id">The ID number of the dialogue option to disable</param>
		 */
		public new void TurnOptionOff (int id)
		{
			foreach (AC.ButtonDialog option in options)
			{
				if (option.ID == id)
				{
					if (!option.isLocked)
					{
						option.isOn = false;
					}
					else
					{
						AC.ACDebug.LogWarning (gameObject.name + "'s option '" + option.label + "' cannot be turned off as it is locked.", this);
					}
					return;
				}
			}
		}


		/**
		 * <summary>Sets the enabled and locked states of a dialogue option, provided that it is unlocked.</summary>
		 * <param name = "id">The ID number of the dialogue option to change</param>
		 * <param name = "flag">The "on/off" state to set the option</param>
		 * <param name = "isLocked">The "locked/unlocked" state to set the option</param>
		 */
		public new void SetOptionState (int id, bool flag, bool isLocked)
		{
		foreach (AC.ButtonDialog option in options)
			{
				if (option.ID == id)
				{
					if (!option.isLocked)
					{
						option.isLocked = isLocked;
						option.isOn = flag;
					}
					AC.KickStarter.playerMenus.RefreshDialogueOptions ();
					return;
				}
			}
		}


		/**
		 * <summary>Turns all dialogue options on</summary>
		 * <param name = "includingLocked">If True, then locked options will be unlocked and turned on as well. Otherwise, they will remain locked</param>
		 */
		public new void TurnAllOptionsOn (bool includingLocked)
		{
			foreach (AC.ButtonDialog option in options)
			{
				if (includingLocked || !option.isLocked)
				{
					option.isLocked = false;
					option.isOn = true;
				}
			}
		}


		/**
		 * <summary>Renames a dialogue option.</summary>
		 * <param name = "id">The ID number of the dialogue option to rename</param>
		 * <param name = "newLabel">The new label text to give the dialogue option<param>
		 * <param name = "newLindID">The line ID number to give the dialogue option, as set by the Speech Manager</param>
		 */
		public new void RenameOption (int id, string newLabel, int newLineID)
		{
			foreach (AC.ButtonDialog option in options)
			{
				if (option.ID == id)
				{
					option.label = newLabel;
					option.lineID = newLineID;
					return;
				}
			}
		}
		

		/**
		 * <summary>Gets the number of enabled dialogue options.</summary>
		 * <returns>The number of enabled dialogue options</summary>
		 */
		public new int GetCount ()
		{
			int numberOn = 0;
			foreach (AC.ButtonDialog _option in options)
			{
				if (_option.CanShow ())
				{
					numberOn ++;
				}
			}
			return numberOn;
		}


		/**
		 * <summary>Checks if a dialogue option with a specific ID is active.</summary>
		 * <param name="ID">The ID of the dialogue option to check for</param>
		 * <returns>True if the specified option is active</summary>
		 */
		public new bool OptionWithIDIsActive (int ID)
		{
			AC.ButtonDialog buttonDialog = GetOptionWithID (ID);
			if (buttonDialog == null) return false;
			return buttonDialog.CanShow ();
		}


		/**
		 * <summmary>Gets an array of ID numbers of existing ButtonDialog classes, so that a unique number can be generated.</summary>
		 * <returns>Gets an array of ID numbers of existing ButtonDialog classes</returns>
		 */
		public new int[] GetIDArray ()
		{
			List<int> idArray = new List<int>();
			foreach (AC.ButtonDialog option in options)
			{
				idArray.Add (option.ID);
			}
			
			idArray.Sort ();
			return idArray.ToArray ();
		}


		/** Checks if the Converations options are currently being overridden by an ActionList */
		public new bool HasActionListOverride ()
		{
			return (overrideActiveList != null);
		}

		
		/** Checks if the Converations options are currently being overridden by a specific ActionList */
		public new bool IsOverridingActionList (AC.ActionList actionList)
		{
			return overrideActiveList != null && overrideActiveList.actionList == actionList;
		}

		#endregion


		#region ProtectedFunctions

		protected void RunOption(ButtonDialogExtended option)
		{
			AC.ACDebug.Log($"[ConversationFromFile] RunOption(ButtonDialogExtended) called with option ID: {option?.ID}", this);
			if (option == null)
			{
				AC.ACDebug.LogWarning("Null option provided", this);
				return;
			}

			ConversationOptionData optionData = option.customData as ConversationOptionData;
			if (optionData == null)
			{
				AC.ACDebug.LogWarning("Option has no custom data", this);
				base.RunOption(option);
				return;
			}

			// Mark the option as chosen
			option.hasBeenChosen = true;

			// Log the current stage and option type
			AC.ACDebug.Log($"Current stage: {stage}, Option type: {optionData.type}", this);

			// Handle the option based on its type
			switch (optionData.type)
			{
				case "question":
					stage = ConversationStage.Response;
					AC.ACDebug.Log("Transitioning to Response stage", this);
					LoadResponseOptions(optionData);
					break;

				case "response":
					stage = ConversationStage.Action;
					AC.ACDebug.Log("Transitioning to Action stage", this);
					LoadActionOptions(optionData);
					break;

				case "action":
					ExecuteAction(optionData);
					stage = ConversationStage.Question;
					AC.ACDebug.Log("Returning to Question stage", this);
					LoadConversationFromJson();
					break;
			}

			// Refresh the menu to show new options
			if (AC.KickStarter.playerMenus != null)
			{
				AC.KickStarter.playerMenus.RefreshDialogueOptions();
			}
		}
		

		protected new void RunDefault ()
		{
			AC.ACDebug.Log("[ConversationFromFile] RunDefault called", this);
			if (AC.KickStarter.playerInput && AC.KickStarter.playerInput.IsInConversation ())
			{
				if (defaultOption < 0 || defaultOption >= options.Count)
				{
					TurnOff ();
				}
				else
				{
					RunOption (defaultOption, true);
				}
			}
		}
		
		
		protected new int ConvertSlotToOption (int slot, bool force = false)
		{
			AC.ACDebug.Log($"[ConversationFromFile] ConvertSlotToOption called with slot: {slot}, force: {force}", this);
			int foundSlots = 0;
			for (int j=0; j<options.Count; j++)
			{
				if (force || options[j].CanShow ())
				{
					foundSlots ++;
					if (foundSlots == (slot+1))
					{
						return j;
					}
				}
			}
			return -1;
		}


		protected new void OnEndActionList (AC.ActionList actionList, AC.ActionListAsset actionListAsset, bool isSkipping)
		{
			AC.ACDebug.Log($"[ConversationFromFile] OnEndActionList called with actionList: {actionList?.name}, isSkipping: {isSkipping}", this);
			if (overrideActiveList == null)
			{
				foreach (AC.ButtonDialog buttonDialog in options)
				{
					if (AC.KickStarter.actionListManager.IsListRunning (buttonDialog.dialogueOption))
					{
						AC.KickStarter.eventManager.Call_OnEndConversation (this);
						return;
					}
				}
			}
		}


		protected new void OnEndConversation (AC.Conversation conversation)
		{
			AC.ACDebug.Log($"[ConversationFromFile] OnEndConversation called with conversation: {conversation?.name}", this);
			if (conversation == this && onFinishActiveList != null)
			{
				if (onFinishActiveList.actionListAsset)
				{
					AC.KickStarter.actionListManager.ResetSkippableData ();
					onFinishActiveList.actionList = AC.AdvGame.RunActionListAsset (onFinishActiveList.actionListAsset, onFinishActiveList.startIndex, true);
				}
				else if (onFinishActiveList.actionList)
				{
					AC.KickStarter.actionListManager.ResetSkippableData ();
					onFinishActiveList.actionList.Interact (onFinishActiveList.startIndex, true);
				}
			}

			onFinishActiveList = null;
		}


		protected new void OnFinishLoading (int saveID)
		{
			AC.ACDebug.Log($"[ConversationFromFile] OnFinishLoading called with saveID: {saveID}", this);
			onFinishActiveList = null;
			overrideActiveList = null;
		}

		private void LoadResponseOptions(ConversationOptionData optionData)
		{
			AC.ACDebug.Log($"[ConversationFromFile] LoadResponseOptions called with objectKey: {optionData?.objectKey}", this);
			if (!string.IsNullOrEmpty(optionData.objectKey))
			{
				options.Clear();
				var outcomes = jsonData?.responses?.outcomes?.items?.GetValueOrDefault(optionData.objectKey);
				if (outcomes != null)
				{
					// Create a list to store all possible responses
					List<(string text, string[] items)> allResponses = new List<(string text, string[] items)>();
					
					// Add left responses
					if (outcomes.left != null)
					{
						foreach (string outcome in outcomes.left)
						{
							allResponses.Add((outcome, outcomes.left_items));
						}
					}
					
					// Add right responses
					if (outcomes.right != null)
					{
						foreach (string outcome in outcomes.right)
						{
							allResponses.Add((outcome, outcomes.right_items));
						}
					}

					// Create options for each response
					foreach (var (text, items) in allResponses)
					{
						var responseData = new ConversationOptionData
						{
							type = "response",
							objectKey = optionData.objectKey,
							validItems = items
						};

						ButtonDialogExtended responseDialog = CreateDialogOption(text, responseData);
						if (responseDialog != null)
						{
							options.Add(responseDialog);
						}
					}
				}
				
				// Log the number of options created
				AC.ACDebug.Log($"Created {options.Count} response options for object {optionData.objectKey}", this);
			}
		}

		private void LoadActionOptions(ConversationOptionData optionData)
		{
			AC.ACDebug.Log($"[ConversationFromFile] LoadActionOptions called with validItems: {string.Join(", ", optionData?.validItems ?? new string[0])}", this);
			if (optionData.validItems != null && optionData.validItems.Length > 0)
			{
				options.Clear();
				foreach (var category in new[] 
				{ 
					new { dict = jsonData.actions.objects, name = "objects" },
					new { dict = jsonData.actions.npcs, name = "npcs" },
					new { dict = jsonData.actions.locations, name = "locations" }
				})
				{
					if (category.dict == null) continue;
					
					foreach (string itemKey in optionData.validItems)
					{
						if (category.dict.TryGetValue(itemKey, out ActionItemData itemData))
						{
							foreach (string action in itemData.actions)
							{
								foreach (string result in itemData.valid_results)
								{
									string actionText = $"Maybe you should {action} {itemData.name}, {result}";
									
									var actionOptionData = new ConversationOptionData
									{
										type = "action",
										objectKey = itemKey,
										validItems = new[] { itemKey }
									};
									
									ButtonDialogExtended actionDialog = CreateDialogOption(actionText, actionOptionData);
									options.Add(actionDialog);
								}
							}
						}
					}
				}
			}
		}

		private void ExecuteAction(ConversationOptionData optionData)
		{
			AC.ACDebug.Log($"[ConversationFromFile] ExecuteAction called with objectKey: {optionData?.objectKey}", this);
			var customAction = CreateCustomAction(optionData);
			if (customAction != null)
			{
				GameObject actionListObject = new GameObject($"Action: {optionData.objectKey}");
				actionListObject.transform.parent = transform;
				AC.ActionList actionList = actionListObject.AddComponent<AC.ActionList>();
				actionList.actions = new List<AC.Action> { customAction };
				actionList.Interact();
			}
		}

		#endregion


		#if UNITY_EDITOR

		/**
		 * <summary>Converts the Conversations's references from a given local variable to a given global variable</summary>
		 * <param name = "oldLocalID">The ID number of the old local variable</param>
		 * <param name = "newGlobalID">The ID number of the new global variable</param>
		 * <returns>True if the Action was amended</returns>
		 */
		public new bool ConvertLocalVariableToGlobal (int oldLocalID, int newGlobalID)
		{
			bool wasAmened = false;

			if (options != null)
			{
				foreach (AC.ButtonDialog option in options)
				{
					string newLabel = AC.AdvGame.ConvertLocalVariableTokenToGlobal (option.label, oldLocalID, newGlobalID);
					if (newLabel != option.label)
					{
						option.label = newLabel;
						wasAmened = true;
					}
				}
			}

			return wasAmened;
		}


		/**
		 * <summary>Gets the number of references to a given variable</summary>
		 * <param name = "location">The location of the variable (Global, Local)</param>
		 * <param name = "varID">The ID number of the variable</param>
		 * <returns>The number of references to the variable</returns>
		 */
		public new int GetNumVariableReferences (AC.VariableLocation location, int varID, AC.Variables variables = null, int _variablesConstantID = 0)
		{
			int numFound = 0;
			if (options != null)
			{
				string tokenText = AC.AdvGame.GetVariableTokenText (location, varID, _variablesConstantID);

				foreach (AC.ButtonDialog option in options)
				{
					if (option.label.ToLower ().Contains (tokenText))
					{
						numFound ++;
					}
				}
			}
			return numFound;
		}


		public new int UpdateVariableReferences (AC.VariableLocation location, int oldVariableID, int newVariableID, AC.Variables variables = null, int variablesConstantID = 0)
		{
			int numFound = 0;
			if (options != null)
			{
				string oldTokenText = AC.AdvGame.GetVariableTokenText (location, oldVariableID, variablesConstantID);
				foreach (AC.ButtonDialog option in options)
				{
					if (option.label.ToLower ().Contains (oldTokenText))
					{
						string newTokenText = AC.AdvGame.GetVariableTokenText (location, newVariableID, variablesConstantID);
						option.label = option.label.Replace (oldTokenText, newTokenText);
						numFound++;
					}
				}
			}
			return numFound;
		}


		public new int GetNumItemReferences (int itemID)
		{
			int numFound = 0;
			foreach (AC.ButtonDialog option in options)
			{
				if (option.linkToInventory && option.linkedInventoryID == itemID)
				{
					numFound ++;
				}
			}
			return numFound;
		}


		public new int UpdateItemReferences (int oldItemID, int newItemID)
		{
			int numFound = 0;
			foreach (AC.ButtonDialog option in options)
			{
				if (option.linkToInventory && option.linkedInventoryID == oldItemID)
				{
					option.linkedInventoryID = newItemID;
					numFound++;
				}
			}
			return numFound;
		}


		/**
		 * <summary>Converts the Conversations's references from a given global variable to a given local variable</summary>
		 * <param name = "oldLocalID">The ID number of the old global variable</param>
		 * <param name = "newLocalID">The ID number of the new local variable</param>
		 * <returns>True if the Action was amended</returns>
		 */
		public new bool ConvertGlobalVariableToLocal (int oldGlobalID, int newLocalID, bool isCorrectScene)
		{
			bool wasAmened = false;

			if (options != null)
			{
				foreach (AC.ButtonDialog option in options)
				{
					string newLabel = AC.AdvGame.ConvertGlobalVariableTokenToLocal (option.label, oldGlobalID, newLocalID);
					if (newLabel != option.label)
					{
						wasAmened = true;
						if (isCorrectScene)
						{
							option.label = newLabel;
						}
					}
				}
			}

			return wasAmened;
		}

		#endif


		#region ITranslatable

		public new string GetTranslatableString (int index)
		{
			return options[index].label;
		}


		public new int GetTranslationID (int index)
		{
			return options[index].lineID;
		}


		public new AC.AC_TextType GetTranslationType (int index)
		{
			return AC.AC_TextType.DialogueOption;
		}


		#if UNITY_EDITOR

		public new void UpdateTranslatableString (int index, string updatedText)
		{
			if (index < options.Count)
			{
				options[index].label = updatedText;
			}
		}


		public new int GetNumTranslatables ()
		{
			if (options != null) return options.Count;
			return 0;
		}


		public new bool HasExistingTranslation (int index)
		{
			return (options[index].lineID > 0);
		}


		public new void SetTranslationID (int index, int _lineID)
		{
			options[index].lineID = _lineID;
		}


		public new string GetOwner (int index)
		{
			return string.Empty;
		}


		public new  bool OwnerIsPlayer (int index)
		{
			return false;
		}


		public new  bool CanTranslate (int index)
		{
			return (!string.IsNullOrEmpty (options[index].label));
		}

		#endif

		#endregion


		#if UNITY_EDITOR

		public new  bool ReferencesAsset (AC.ActionListAsset actionListAsset)
		{
			if (interactionSource == AC.InteractionSource.AssetFile)
			{
				foreach (AC.ButtonDialog buttonDialog in options)
				{
					if (buttonDialog.assetFile == actionListAsset) return true;
				}
			}
			return false;
		}


		public new  List<AC.ActionListAsset> GetReferencedActionListAssets ()
		{
			if (interactionSource == AC.InteractionSource.AssetFile)
			{
				List<AC.ActionListAsset> assets = new List<AC.ActionListAsset> ();
				for (int i = 0; i < options.Count; i++)
				{
					assets.Add (options[i].assetFile);
				}
				return assets;
			}
			return null;
		}

		#endif


		public new  AC.MenuDialogList LinkedDialogList
		{
			get => linkedDialogList;
			set => linkedDialogList = value;
		}

		protected virtual void LoadConversationFromJson()
		{
			AC.ACDebug.Log("[ConversationFromFile] LoadConversationFromJson called", this);
			try
			{
				if (jsonFile == null)
				{
					AC.ACDebug.LogWarning("No JSON file assigned", this);
					return;
				}

				string jsonString = jsonFile.text;
				if (string.IsNullOrEmpty(jsonString))
				{
					AC.ACDebug.LogWarning("JSON file is empty", this);
					return;
				}

				AC.ACDebug.Log($"Loading JSON content: {jsonString}", this);
				
				// Initialize jsonData and its properties
				jsonData = new ConversationData
				{
					responses = new ResponseData
					{
						acknowledgements = new string[0],
						speculations = new string[0],
						outcomes = new OutcomeDictionary()
					},
					actions = new ActionData
					{
						objects = new Dictionary<string, ActionItemData>(),
						npcs = new Dictionary<string, ActionItemData>(),
						locations = new Dictionary<string, ActionItemData>()
					}
				};
				
				// Parse the objects dictionary first - remove only newlines and carriage returns, keep spaces
				var objectDict = ObjectDictionary.CreateFromJSON(jsonString);
				if (objectDict == null || objectDict.items == null || objectDict.items.Count == 0)
				{
					AC.ACDebug.LogWarning("Failed to parse objects data", this);
					return;
				}

				// Parse question structure
				int structureStart = jsonString.IndexOf("\"question_structure\":\"") + 21;
				int structureEnd = jsonString.IndexOf("\"", structureStart);
				if (structureStart >= 21 && structureEnd != -1)
				{
					jsonData.question_structure = jsonString.Substring(structureStart, structureEnd - structureStart);
				}
				else
				{
					jsonData.question_structure = "Tell me, why [observation] Is it because [cause] [mystical_question]";
				}
				
				// Parse responses
				jsonData.responses.acknowledgements = ParseStringArray(jsonString, "acknowledgements");
				jsonData.responses.speculations = ParseStringArray(jsonString, "speculations");
				jsonData.responses.outcomes = OutcomeDictionary.CreateFromJSON(jsonString);
				
				// Parse actions section
				int actionsStart = jsonString.IndexOf("\"actions\":{");
				if (actionsStart != -1)
				{
					actionsStart = jsonString.IndexOf("{", actionsStart);
					int actionsEnd = FindMatchingBrace(jsonString, actionsStart);
					if (actionsEnd != -1)
					{
						string actionsJson = jsonString.Substring(actionsStart, actionsEnd - actionsStart + 1);
						jsonData.actions = JsonUtility.FromJson<ActionData>(actionsJson) ?? new ActionData();
					}
				}

				options.Clear();
				
				// Generate all possible question combinations
				List<(string objectKey, string questionText)> allCombinations = new List<(string, string)>();
				
				foreach (var kvp in objectDict.items)
				{
					string objectKey = kvp.Key;
					ObjectData objectData = kvp.Value;
					
					if (objectData?.observations == null || objectData.causes == null || objectData.mystical_questions == null)
					{
						AC.ACDebug.LogWarning($"Skipping invalid object data for key: {objectKey}", this);
						continue;
					}

					foreach (string observation in objectData.observations)
					{
						foreach (string cause in objectData.causes)
						{
							foreach (string mysticalQuestion in objectData.mystical_questions)
							{
								if (string.IsNullOrEmpty(observation) || string.IsNullOrEmpty(cause) || string.IsNullOrEmpty(mysticalQuestion))
								{
									continue;
								}

								string questionText = jsonData.question_structure
									.Replace("[observation]", observation.Trim())
									.Replace("[cause]", cause.Trim())
									.Replace("[mystical_question]", mysticalQuestion.Trim())
									.Replace("  ", " ") // Remove any double spaces
									.Trim(); // Remove leading/trailing spaces
								
								allCombinations.Add((objectKey, questionText));
							}
						}
					}
				}

				// Randomly select combinations and create dialog options
				options.Clear();
				int numOptionsToShow = Math.Min(3, allCombinations.Count);
				
				for (int i = 0; i < numOptionsToShow; i++)
				{
					int randomIndex = UnityEngine.Random.Range(0, allCombinations.Count);
					var selectedCombination = allCombinations[randomIndex];
					
					var optionData = new ConversationOptionData
					{
						type = "question",
						objectKey = selectedCombination.objectKey
					};
					
					ButtonDialogExtended questionDialog = CreateDialogOption(selectedCombination.questionText, optionData);
					if (questionDialog != null)
					{
						options.Add(questionDialog);
					}
					
					allCombinations.RemoveAt(randomIndex);
				}
			}
			catch (System.Exception e)
			{
				AC.ACDebug.LogWarning($"Failed to load conversation from JSON: {e.Message}\nStack trace: {e.StackTrace}", this);
			}
		}

		private string[] ParseStringArray(string jsonString, string arrayName)
		{
			try
			{
				int start = jsonString.IndexOf($"\"{arrayName}\":[") + arrayName.Length + 4;
				int end = jsonString.IndexOf("]", start);
				
				if (start < arrayName.Length + 4 || end == -1) return new string[0];
				
				string arrayJson = jsonString.Substring(start, end - start);
				string[] items = arrayJson.Split(',')
					.Select(s => s.Trim().Trim('"'))
					.Where(s => !string.IsNullOrEmpty(s))
					.ToArray();
					
				return items;
			}
			catch
			{
				return new string[0];
			}
		}

		private ButtonDialogExtended CreateDialogOption(string text, ConversationOptionData optionData)
		{
			AC.ACDebug.Log($"[ConversationFromFile] CreateDialogOption called with text: {text}, type: {optionData?.type}", this);
			lastGeneratedId++;
			int newId = lastGeneratedId;
			int[] newIdArray = new int[] { newId };
			
			ButtonDialogExtended dialog = new ButtonDialogExtended(newIdArray);
			dialog.ID = newId;
			dialog.label = text;
			dialog.isOn = true;

			// Create the DialogueOption ActionList
			GameObject actionListObject = new GameObject($"Dialogue Option {newId}: {text}");
			actionListObject.transform.parent = transform;
			AC.DialogueOption dialogueOption = actionListObject.AddComponent<AC.DialogueOption>();
			
			// Set up the actions based on the option type
			dialogueOption.actions = new List<AC.Action>();
			dialogueOption.actionListType = AC.ActionListType.PauseGameplay;
			
			dialog.dialogueOption = dialogueOption;
			dialog.customData = optionData;
			dialog.linkToInventory = false;
			
			return dialog;
		}

		private void AddResponseOption(string outcome, string[] validItems, string objectKey)
		{
			AC.ACDebug.Log($"[ConversationFromFile] AddResponseOption called with outcome: {outcome}, objectKey: {objectKey}", this);
			string acknowledgement = GetRandomElement(jsonData.responses.acknowledgements);
			string speculation = GetRandomElement(jsonData.responses.speculations);
			
			string responseText = $"Ah, {acknowledgement} {outcome} {speculation}";
			
			var optionData = new ConversationOptionData
			{
				type = "response",
				objectKey = objectKey,
				validItems = validItems
			};
			
			ButtonDialogExtended responseDialog = CreateDialogOption(responseText, optionData);
			options.Add(responseDialog);
		}

		private AC.Action CreateCustomAction(ConversationOptionData optionData)
		{
			AC.ACDebug.Log($"[ConversationFromFile] CreateCustomAction called with objectKey: {optionData?.objectKey}", this);
			// Here you would create a custom Action to handle the specific game logic
			// For now, we'll just create a dummy action that logs the selection
			var customAction = new AC.ActionComment();
			customAction.comment = $"Selected action for {optionData.objectKey}";
			return customAction;
		}

		private T GetRandomElement<T>(T[] array)
		{
			if (array == null || array.Length == 0) return default(T);
			return array[UnityEngine.Random.Range(0, array.Length)];
		}

		protected static int FindMatchingBrace(string text, int openBracePos)
		{
			int braceCount = 1;
			for (int i = openBracePos + 1; i < text.Length; i++)
			{
				if (text[i] == '{') braceCount++;
				else if (text[i] == '}')
				{
					braceCount--;
					if (braceCount == 0) return i;
				}
			}
			return -1;
		}

		[System.Serializable]
		private class ConversationOptionData
		{
			public string type;
			public string objectKey;
			public string[] validItems;
		}

		[System.Serializable]
		private class ConversationData
		{
			public string question_structure;
			public ResponseData responses;
			public ActionData actions;
		}

		[System.Serializable]
		private class ResponseData
		{
			public string[] acknowledgements;
			public OutcomeDictionary outcomes;
			public string[] speculations;
		}

		[System.Serializable]
		private class OutcomeDictionary
		{
			public Dictionary<string, OutcomeValue> items = new Dictionary<string, OutcomeValue>();

			public static OutcomeDictionary CreateFromJSON(string inputJson)
			{
				try
				{
					var wrapper = new OutcomeDictionary();
					
					string cleanJson = inputJson.Replace("\n", "").Replace("\r", "").Replace(" ", "");
					
					int outcomesStart = cleanJson.IndexOf("\"outcomes\":");
					if (outcomesStart == -1) return wrapper;
					
					outcomesStart = cleanJson.IndexOf("{", outcomesStart);
					if (outcomesStart == -1) return wrapper;
					
					int outcomesEnd = FindMatchingBrace(cleanJson, outcomesStart);
					if (outcomesEnd == -1) return wrapper;
					
					string outcomesJson = cleanJson.Substring(outcomesStart, outcomesEnd - outcomesStart + 1);
					
					int currentPos = 1;
					while (currentPos < outcomesJson.Length)
					{
						int keyStart = outcomesJson.IndexOf("\"", currentPos);
						if (keyStart == -1) break;
						
						int keyEnd = outcomesJson.IndexOf("\"", keyStart + 1);
						if (keyEnd == -1) break;
						
						string key = outcomesJson.Substring(keyStart + 1, keyEnd - keyStart - 1);
						
						int valueStart = outcomesJson.IndexOf("{", keyEnd);
						if (valueStart == -1) break;
						
						int valueEnd = FindMatchingBrace(outcomesJson, valueStart);
						if (valueEnd == -1) break;
						
						string valueJson = outcomesJson.Substring(valueStart, valueEnd - valueStart + 1);
						
						var outcome = JsonUtility.FromJson<OutcomeValue>(valueJson);
						if (outcome != null)
						{
							wrapper.items.Add(key, outcome);
						}
						
						currentPos = valueEnd + 1;
					}
					
					return wrapper;
				}
				catch (System.Exception e)
				{
					AC.ACDebug.LogWarning($"Error parsing outcomes: {e.Message}");
					return new OutcomeDictionary();
				}
			}
		}

		[System.Serializable]
		private class ObjectData
		{
			public string[] observations;
			public string[] causes;
			public string[] mystical_questions;
			public string[] actions;
		}

		[System.Serializable]
		private class ActionData
		{
			public Dictionary<string, ActionItemData> objects;
			public Dictionary<string, ActionItemData> npcs;
			public Dictionary<string, ActionItemData> locations;
		}

		[System.Serializable]
		private class ActionItemData
		{
			public string name;
			public string[] actions;
			public string[] valid_results;
		}

		[System.Serializable]
		private class OutcomeValue
		{
			public string[] left;
			public string[] left_items;
			public string[] right;
			public string[] right_items;
		}

		[System.Serializable]
		private class ObjectDictionary
		{
			public Dictionary<string, ObjectData> items = new Dictionary<string, ObjectData>();

			public static ObjectDictionary CreateFromJSON(string inputJson)
			{
				try
				{
					var wrapper = new ObjectDictionary();
					
					// Only remove newlines and carriage returns, preserve spaces
					string cleanJson = inputJson.Replace("\n", "").Replace("\r", "");
					
					int objectsStart = cleanJson.IndexOf("\"objects\":");
					if (objectsStart == -1) return wrapper;
					
					objectsStart = cleanJson.IndexOf("{", objectsStart);
					if (objectsStart == -1) return wrapper;
					
					int objectsEnd = FindMatchingBrace(cleanJson, objectsStart);
					if (objectsEnd == -1) return wrapper;
					
					string objectsJson = cleanJson.Substring(objectsStart, objectsEnd - objectsStart + 1);
					
					int currentPos = 1;
					while (currentPos < objectsJson.Length)
					{
						int keyStart = objectsJson.IndexOf("\"", currentPos);
						if (keyStart == -1) break;
						
						int keyEnd = objectsJson.IndexOf("\"", keyStart + 1);
						if (keyEnd == -1) break;
						
						string key = objectsJson.Substring(keyStart + 1, keyEnd - keyStart - 1);
						
						int valueStart = objectsJson.IndexOf("{", keyEnd);
						if (valueStart == -1) break;
						
						int valueEnd = FindMatchingBrace(objectsJson, valueStart);
						if (valueEnd == -1) break;
						
						string valueJson = objectsJson.Substring(valueStart, valueEnd - valueStart + 1);
						
						var objectData = JsonUtility.FromJson<ObjectData>(valueJson);
						if (objectData != null)
						{
							wrapper.items.Add(key, objectData);
						}
						
						currentPos = valueEnd + 1;
					}
					
					return wrapper;
				}
				catch (System.Exception e)
				{
					AC.ACDebug.LogWarning($"Error parsing objects: {e.Message}");
					return new ObjectDictionary();
				}
			}
		}
	}
}
