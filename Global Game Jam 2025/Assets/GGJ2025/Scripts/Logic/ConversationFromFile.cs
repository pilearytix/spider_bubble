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
using System.Linq;
using System;
using UnityEngine.UI;
using Unity.VisualScripting;
using AC;
using GGJ2025.Models;
using GGJ2025.Systems;
using GGJ2025.Utils;

namespace GGJ2025
{    
	[Serializable]
	public class ButtonDialogExtended : AC.ButtonDialog 
	{
		public object customData;
		public bool hasBeenUsed;

		public ButtonDialogExtended(int[] idArray) : base(idArray) 
		{
			hasBeenUsed = false;
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

			
		}

		private void OnEnable()
		{
			AC.ACDebug.Log("[ConversationFromFile] OnEnable called", this);
			

			if (stage == ConversationStage.Question || stage == ConversationStage.Action && loadFromJson && jsonFile != null)
			{
				// Add event subscriptions
				AC.EventManager.OnClickConversation -= OnClickConversation;  // Always unsubscribe first
				AC.EventManager.OnClickConversation += OnClickConversation;
				
				AC.EventManager.OnEndActionList += OnEndActionList;
				AC.EventManager.OnEndConversation += OnEndConversation;
				AC.EventManager.OnFinishLoading += OnFinishLoading;
				LoadConversationFromJson();
			}

		}

		private void OnDisable()
		{
			// Clean up all event subscriptions
			AC.EventManager.OnClickConversation -= OnClickConversation;
			AC.EventManager.OnEndActionList -= OnEndActionList;
			AC.EventManager.OnEndConversation -= OnEndConversation;
			AC.EventManager.OnFinishLoading -= OnFinishLoading;
		}

		private void OnClickConversation(AC.Conversation conversation, int optionID)
		{
			if (conversation == this && loadFromJson && jsonFile != null)
			{

				// Find the clicked option
				ButtonDialogExtended buttonDialog = null;
				foreach (var option in options)
				{
					option.isOn = false;
					if (option.ID == optionID)
					{					
						option.hasBeenChosen = true;

						// Process inventory items BEFORE marking as used
						ProcessTextForInventoryItems(option.label);						

						// Now mark as used and store the button dialog
						if (option is ButtonDialogExtended extendedOption)
						{
							buttonDialog = extendedOption;
							buttonDialog.hasBeenUsed = true;
							AC.ACDebug.Log($"Marked option '{option.label}' as used", this);
						}
					}
				}

				// Rest of the switch statement for handling option types
				if (buttonDialog != null)
				{
					var optionData = buttonDialog.customData as ConversationOptionData;
					AC.ACDebug.Log($"Option data: {optionData}", this);
					switch (optionData?.type)
					{
						case "response":
							// After showing response, return to question stage
							stage = ConversationStage.Question;
							UpdateOptionVisibility();
							break;
							
						case "simple":
							AC.ACDebug.Log($"Simple conversation option data: {optionData}", this);
							ExecuteSimpleConversation(optionData);
							break;
							
						case "question":
							LoadResponseOptions(optionData);
							break;
							
						default:
							AC.ACDebug.LogWarning($"Unknown option type: {optionData?.type}", this);
							break;
					}
				}
			}
			
		}

		#region Variables

		[Header("JSON Configuration")]
		[SerializeField] private TextAsset jsonFile;
		[Tooltip("If true, will load conversation options from the JSON file when enabled")]
		[SerializeField] private bool loadFromJson = false;
		[SerializeField] private ConversationStage stage = ConversationStage.Question;

		private GGJ2025.Models.JsonConversationData jsonData;
		private int lastGeneratedId = 0;  // Track the last generated ID

		private readonly Dictionary<ConversationStage, Action<ConversationOptionData>> stageHandlers;

		#endregion

		#region UnityStandards

		private void Start ()
		{
			// AC.ACDebug.Log("ConversationFromFile Start called", this);
			// if (AC.KickStarter.inventoryManager)
			// {
			// 	foreach (AC.ButtonDialog option in options)
			// 	{
			// 		if (option.linkToInventory && option.cursorIcon.texture == null)
			// 		{
			// 			AC.InvItem linkedItem = AC.KickStarter.inventoryManager.GetItem (option.linkedInventoryID);
			// 			if (linkedItem != null && linkedItem.tex != null)
			// 			{
			// 				option.cursorIcon.ReplaceTexture (linkedItem.tex);
			// 			}
			// 		}
			// 	}
			// }
		}

		#endregion

		#region ProtectedFunctions


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

		private string FormatDialogueText(params string[] parts)
		{
			// Filter out null or empty strings but preserve internal spaces
			var validParts = parts
				.Where(p => !string.IsNullOrEmpty(p))
				.Select(p => {
					// Normalize internal spaces (replace multiple spaces with single space)
					string normalized = System.Text.RegularExpressions.Regex.Replace(p.Trim(), @"\s+", " ");
					return normalized;
				})
				.Where(p => !string.IsNullOrWhiteSpace(p));
			
			// Join with single spaces between parts
			return string.Join(" ", validParts);
		}

		private void LoadResponseOptions(ConversationOptionData optionData)
		{
			UpdateOptionVisibility();

			AC.ACDebug.Log($"[ConversationFromFile] LoadResponseOptions called with objectKey: {optionData?.objectKey}", this);
			if (!string.IsNullOrEmpty(optionData.objectKey))
			{
				// Count existing options for this objectKey
				int existingOptionsCount = options.Count(o => 
					(o as ButtonDialogExtended)?.customData is ConversationOptionData data && 
					data.objectKey == optionData.objectKey);

				if (existingOptionsCount >= 8)
				{
					AC.ACDebug.Log($"Already have {existingOptionsCount} options for {optionData.objectKey}, skipping new options", this);
					stage = ConversationStage.Response;
					UpdateOptionVisibility();
					return;
				}

				// Change stage to show response
				stage = ConversationStage.Response;
				
				// Get outcomes for the specific object
				if (jsonData.responses.outcomes.items.TryGetValue(optionData.objectKey, out var outcomes) && outcomes != null)
				{
					List<(string outcome, string[] validItems)> allOutcomes = new List<(string, string[])>();
					
					// Collect all possible outcomes
					if (outcomes.left != null)
					{
						allOutcomes.AddRange(outcomes.left.Select(o => (o, outcomes.left_items)));
					}
					if (outcomes.right != null)
					{
						allOutcomes.AddRange(outcomes.right.Select(o => (o, outcomes.right_items)));
					}

					// Randomly select outcomes up to the remaining slot limit
					int slotsRemaining = 8 - existingOptionsCount;
					int outcomesToAdd = Math.Min(slotsRemaining, allOutcomes.Count);
					
					// Shuffle the outcomes
					allOutcomes = allOutcomes.OrderBy(x => UnityEngine.Random.value).ToList();

					// Add the selected number of outcomes
					for (int i = 0; i < outcomesToAdd; i++)
					{
						var (outcome, validItems) = allOutcomes[i];
						var buttonDialog = new ButtonDialogExtended(new int[] { 0 });
						buttonDialog.label = FormatDialogueText(
							GetRandomElement(jsonData.responses.acknowledgements),
							outcome,
							GetRandomElement(outcomes.speculations)
						);
						buttonDialog.customData = new ConversationOptionData { 
							type = "response",
							objectKey = optionData.objectKey,
							validItems = validItems
						};
						options.Add(buttonDialog);
					}
				}
				
				UpdateOptionVisibility();
			}
		}

		private void ExecuteSimpleConversation(ConversationOptionData optionData)
		{
			AC.ACDebug.Log($"[ConversationFromFile] ExecuteSimpleConversation called with objectKey: {optionData?.objectKey}", this);
			AC.ACDebug.Log($"[ConversationFromFile] ExecuteSimpleConversation called with answer: {optionData?.answer}", this);

			// Create a response dialog option
			// check if it already exists in the current options list:
			var existingOption = options.FirstOrDefault(o => o.label == optionData.answer);
			if (existingOption != null)
			{
				AC.ACDebug.Log($"[ConversationFromFile] ExecuteSimpleConversation called with existing option: {existingOption.label}", this);
				return;
			}

			// if it doesn't exist, create a new one

			lastGeneratedId++;
			int newId = lastGeneratedId;
			int[] newIdArray = new int[] { newId };
			
			ButtonDialogExtended dialog = new ButtonDialogExtended(newIdArray);
			dialog.ID = newId;
			dialog.label = optionData.answer;
			dialog.customData = new ConversationOptionData { type = "response" };
			options.Add(dialog);

			// Change stage to show response
			stage = ConversationStage.Response;
			
			UpdateOptionVisibility();
		}

		private void LoadActionOptions(ConversationOptionData optionData)
		{
			AC.ACDebug.Log($"[ConversationFromFile] LoadActionOptions called with validItems: {string.Join(", ", optionData?.validItems ?? new string[0])}", this);
			if (optionData.validItems != null && optionData.validItems.Length > 0)
			{
				UpdateOptionVisibility();
				
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
			bool wasAmended = false;

			if (options != null)
			{
				foreach (AC.ButtonDialog option in options)
				{
					string newLabel = AC.AdvGame.ConvertLocalVariableTokenToGlobal (option.label, oldLocalID, newGlobalID);
					if (newLabel != option.label)
					{
						option.label = newLabel;
						wasAmended = true;
					}
				}
			}

			return wasAmended;
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
			// hide all the options
			foreach (var option in options)
			{
				option.isOn = false;
			}

			AC.ACDebug.Log("[ConversationFromFile] LoadConversationFromJson called", this);
			try
			{
				UpdateOptionVisibility();
				
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
				jsonData = new JsonConversationData
				{
					conversations = new ConversationEntry[0],
					responses = new ResponseData
					{
						acknowledgements = new string[0],
						outcomes = new OutcomeDictionary()
					},
					actions = new ActionData
					{
						objects = new Dictionary<string, ActionItemData>(),
						npcs = new Dictionary<string, ActionItemData>(),
						locations = new Dictionary<string, ActionItemData>()
					}
				};

				var conversations = JsonUtility.FromJson<JsonConversationData>(jsonString);
				jsonData.conversations = conversations.conversations;

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
					jsonData.question_structure = "[observation] Is it because [cause]";
				}
				
				// Parse responses
				jsonData.responses.acknowledgements = ParseStringArray(jsonString, "acknowledgements");
				jsonData.responses.outcomes = OutcomeDictionary.CreateFromJSON(jsonString);
				
				// Parse actions section
				int actionsStart = jsonString.IndexOf("\"actions\":{");
				if (actionsStart != -1)
				{
					actionsStart = jsonString.IndexOf("{", actionsStart);
					int actionsEnd = JsonParser.FindMatchingBrace(jsonString, actionsStart);
					if (actionsEnd != -1)
					{
						string actionsJson = jsonString.Substring(actionsStart, actionsEnd - actionsStart + 1);
						jsonData.actions = JsonUtility.FromJson<ActionData>(actionsJson) ?? new ActionData();
					}
				}

				// Modify the numOptionToShow calculation
				int numOptionsToShow = 8; // Always show 3 of each option, regardless of combinations count

				// Add simple conversation options
				for (int i = 0; i < numOptionsToShow; i++)
				{
					// Get available indices (ones we haven't used yet)
					var availableIndices = Enumerable.Range(0, jsonData.conversations.Length)
						.Where(idx => !ConversationTracker.IsConversationUsed(idx))
						.ToList();

					// If we've used all conversations, reset the tracking
					if (availableIndices.Count == 0)
					{
						ConversationTracker.ResetTracking();
						availableIndices = Enumerable.Range(0, jsonData.conversations.Length).ToList();
					}

					// Pick a random index from available ones
					int randomIndex = UnityEngine.Random.Range(0, availableIndices.Count);
					int selectedIndex = availableIndices[randomIndex];
					
					var selectedSimpleConversation = jsonData.conversations[selectedIndex];
					ConversationTracker.MarkConversationUsed(selectedIndex);
					
					var simpleOptionData = new ConversationOptionData
					{
						type = "simple",
						answer = selectedSimpleConversation.answer
					};

					ButtonDialogExtended simpleDialog = CreateDialogOption(
						selectedSimpleConversation.question, 
						simpleOptionData
					);
					options.Add(simpleDialog);

					// Add a complex question option - pick a random object from the dictionary
					if (jsonData.responses.outcomes.items != null && jsonData.responses.outcomes.items.Count > 0)
					{
						var objectKeys = jsonData.responses.outcomes.items.Keys.ToList();
						string randomObjectKey = objectKeys[UnityEngine.Random.Range(0, objectKeys.Count)];
						
						var optionData = new ConversationOptionData
						{
							type = "question",
							objectKey = randomObjectKey
						};

						// get the causes from the outcomes dictionary
		

						// get random cause
						var randomCause = GetRandomElement(objectDict.items[randomObjectKey].causes);
						var randomObservation = GetRandomElement(objectDict.items[randomObjectKey].observations);
						string questionText = jsonData.question_structure
							.Replace("[observation]", randomObservation)
							.Replace("[cause]", randomCause);

						ButtonDialogExtended questionDialog = CreateDialogOption(
							questionText, 
							optionData
						);
						if (questionDialog != null)
						{
							options.Add(questionDialog);
						}
					}
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

		private void ProcessTextForInventoryItems(string text)
		{
			if (string.IsNullOrEmpty(text)) return;

			// Find all matches of text within <u> tags
			var matches = System.Text.RegularExpressions.Regex.Matches(text, @"<u>(.*?)</u>");
			
			foreach (System.Text.RegularExpressions.Match match in matches)
			{
				string itemName = match.Groups[1].Value.Trim();
				if (!string.IsNullOrEmpty(itemName))
				{
					// Find the item in the inventory manager
					AC.InvItem itemToAdd = AC.KickStarter.inventoryManager.GetItem(itemName);
					if (itemToAdd != null)
					{
						// Add the item to the player's inventory using the item's ID
						AC.KickStarter.runtimeInventory.Add(itemToAdd.id);
						AC.ACDebug.Log($"Added item '{itemName}' to player's inventory", this);
					}
					else
					{
						AC.ACDebug.LogWarning($"Could not find item '{itemName}' in inventory manager", this);
					}
				}
			}
		}

		private ButtonDialogExtended CreateDialogOption(string text, ConversationOptionData optionData)
		{
			AC.ACDebug.Log($"[ConversationFromFile] CreateDialogOption called with text: {text}, type: {optionData?.type}", this);
			
			// Process text for inventory items before creating dialog
			ProcessTextForInventoryItems(text);
			
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

			// Add a comment action
			var commentAction = ScriptableObject.CreateInstance<AC.ActionComment>();
			commentAction.comment = "Run this conversation again";
			dialogueOption.actions.Add(commentAction);
			
			// Run this conversation using properly instantiated ActionConversation
			var runConversationAction = ScriptableObject.CreateInstance<AC.ActionConversation>();
			runConversationAction.conversation = this;
			dialogueOption.actions.Add(runConversationAction);

			dialog.dialogueOption = dialogueOption;
			dialog.customData = optionData;
			dialog.linkToInventory = false;
			
			
			return dialog;
		}

		private void AddResponseOption(string outcome, string[] validItems, string objectKey)
		{
			AC.ACDebug.Log($"[ConversationFromFile] AddResponseOption called with outcome: {outcome}, objectKey: {objectKey}", this);
			string acknowledgement = GetRandomElement(jsonData.responses.acknowledgements)?.Trim() ?? "";
			
			// Get speculations from the outcomes data using the objectKey
			string[] speculations = jsonData?.responses?.outcomes?.items?.GetValueOrDefault(objectKey)?.speculations ?? Array.Empty<string>();
			string speculation = GetRandomElement(speculations)?.Trim() ?? "";
			
			string responseText = FormatDialogueText(acknowledgement, outcome, speculation);
			
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
			// Create the action using ScriptableObject.CreateInstance instead of new
			var customAction = ScriptableObject.CreateInstance<AC.ActionComment>();
			customAction.comment = $"Selected action for {optionData.objectKey}";
			return customAction;
		}

		private T GetRandomElement<T>(T[] array)
		{
			if (array == null || array.Length == 0) return default(T);
			return array[UnityEngine.Random.Range(0, array.Length)];
		}


		protected new void OnEndConversation(AC.Conversation conversation)
		{
			AC.ACDebug.Log($"[ConversationFromFile] OnEndConversation called with conversation: {conversation?.name}", this);
			if (conversation == this)
			{
				stage = ConversationStage.Question;
				if (loadFromJson && jsonFile != null)
				{
					// Check if we need to reset the conversation tracking
					if (ConversationTracker.HasUsedAllConversations)
					{
						ConversationTracker.ResetTracking();
					}
				}
			}
		}

		private void UpdateOptionVisibility()
		{
			// Log current stage and options count
			AC.ACDebug.Log($"[ConversationFromFile] Updating visibility for {options.Count} options in stage {stage}", this);

			foreach (var option in options)
			{
				if (option is ButtonDialogExtended extendedOption)
				{
					var optionData = extendedOption.customData as ConversationOptionData;

					// Default to hidden
					extendedOption.isOn = false;

					// Check if option matches current stage
					bool isMatchingStage = stage switch
					{
						ConversationStage.Question => optionData?.type == "question" || optionData?.type == "simple",
						ConversationStage.Response => optionData?.type == "response",
						ConversationStage.Action => optionData?.type == "action",
						ConversationStage.Simple_Answer => optionData?.type == "simple",
						_ => false
					};
					
					// Only show if matches stage and hasn't been used
					extendedOption.isOn = isMatchingStage;
					
					if (extendedOption.hasBeenChosen)
					{
						extendedOption.isOn = false;
					}

					// Log visibility decision
					AC.ACDebug.Log($"Option '{option.label}' visibility set to {extendedOption.isOn} " +
								  $"(type: {optionData?.type}, stage: {stage}, used: {extendedOption.hasBeenUsed})", this);
				}
			}
		}
	}
}
