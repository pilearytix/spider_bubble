﻿/*
 *
 *	Adventure Creator
 *	by Chris Burton, 2013-2024
 *	
 *	"RuntimeLanguage.cs"
 * 
 *	This script contains all language data for the game at runtime.
 *	It transfers data from the Speech Manaager to itself when the game begins.
 * 
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

#if LocalizationIsPresent && AddressableIsPresent
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Tables;
#endif

namespace AC
{

	/**
	 * This script contains all language data for the game at runtime.
 	 * It transfers data from the Speech Manaager to itself when the game begins.
 	 */
	[HelpURL("https://www.adventurecreator.org/scripting-guide/class_a_c_1_1_runtime_languages.html")]
	public class RuntimeLanguages : MonoBehaviour
	{

		#region Variables

		protected Dictionary<int, SpeechLine> speechLinesDictionary = null;
		protected List<Language> languages = new List<Language>();

		protected AssetBundle currentAudioAssetBundle = null;
		protected string currentAudioAssetBundleName;
		protected AssetBundle currentLipsyncAssetBundle = null;
		protected string currentLipsyncAssetBundleName;

		protected bool isLoadingBundle;

		protected List<int> spokenOnceSpeechLineIDs = new List<int>();
		private SpeechLine speechLine;

		#endregion


		#region PublicFunctions

		public void OnInitPersistentEngine ()
		{
			TransferFromManager ();
		}


		/**
		 * <summary>Loads in audio and lipsync AssetBundles for a given language</summary>
		 * <param name = "language">The index number of the language to load AssetBundles for</param>
		 */
		public virtual void LoadAssetBundle (int language)
		{
			if (KickStarter.speechManager.referenceSpeechFiles == ReferenceSpeechFiles.ByDirectReference)
			{
				// Only reset if necessary
				speechLinesDictionary = new Dictionary<int, SpeechLine> ();
				foreach (SpeechLine speechLine in KickStarter.speechManager.lines)
				{
					if (KickStarter.speechManager.IsTextTypeTranslatable (speechLine.textType))
					{
						speechLinesDictionary.Add (speechLine.lineID, new SpeechLine (speechLine, language));
					}
				}
			}

			if (KickStarter.speechManager.referenceSpeechFiles == ReferenceSpeechFiles.ByAssetBundle)
			{
				StopAllCoroutines ();
				StartCoroutine (LoadAssetBundleCoroutine (language));
			}
		}


		/**
		 * <summary>Gets the AudioClip associated with a speech line</summary>
		 * <param name = "lineID">The ID number of the speech line, as generated by the Speech Manager</param>
		 * <param name = "_speaker">The character speaking the line</param>
		 * <returns>Gets the AudioClip associated with a speech line</returns> 
		 */
		public virtual AudioClip GetSpeechAudioClip (int lineID, Char _speaker)
		{
			if (!KickStarter.speechManager.IsTextTypeTranslatable (AC_TextType.Speech))
			{
				return null;
			}

			int voiceLanguage = Options.GetVoiceLanguage ();
			string voiceLanguageName = (voiceLanguage > 0) ? Options.GetVoiceLanguageName () : string.Empty;

			switch (KickStarter.speechManager.referenceSpeechFiles)
			{
				case ReferenceSpeechFiles.ByNamingConvention:
					{
						string fullName = KickStarter.speechManager.GetAutoAssetPathAndName (lineID, _speaker, voiceLanguageName, false);
						AudioClip clipObj = Resources.Load (fullName) as AudioClip;

						if (clipObj == null && KickStarter.speechManager.fallbackAudio && voiceLanguage > 0)
						{
							fullName = KickStarter.speechManager.GetAutoAssetPathAndName (lineID, _speaker, string.Empty, false);
							clipObj = Resources.Load (fullName) as AudioClip;
						}

						if (clipObj == null && !string.IsNullOrEmpty (fullName))
						{
							ACDebug.LogWarning ("Audio file 'Resources/" + fullName + "' not found in Resources folder.");
						}
						return clipObj;
					}

				case ReferenceSpeechFiles.ByAssetBundle:
					{
						if (isLoadingBundle)
						{
							ACDebug.LogWarning ("Cannot load audio file from AssetBundle as the AssetBundle is still being loaded.");
							return null;
						}
						string fullName = KickStarter.speechManager.GetAutoAssetPathAndName (lineID, _speaker, voiceLanguageName, false);

						int indexOfLastSlash = fullName.LastIndexOf ("/") + 1;
						if (indexOfLastSlash > 0)
						{
							fullName = fullName.Substring (indexOfLastSlash);
						}

						if (currentAudioAssetBundle == null)
						{
							ACDebug.LogWarning ("Cannot load audio file '" + fullName + "' from AssetBundle as no AssetBundle is currently loaded.");
							return null;
						}

						AudioClip clipObj = currentAudioAssetBundle.LoadAsset<AudioClip> (fullName);

						if (clipObj == null && !string.IsNullOrEmpty (fullName))
						{
							ACDebug.LogWarning ("Audio file '" + fullName + "' not found in Asset Bundle '" + currentAudioAssetBundle.name + "'.");
						}
						return clipObj;
					}
					
				case ReferenceSpeechFiles.ByDirectReference:
					{
						AudioClip clipObj = GetLineCustomAudioClip (lineID, voiceLanguage);

						if (clipObj == null && KickStarter.speechManager.fallbackAudio && voiceLanguage > 0)
						{
							return GetLineCustomAudioClip (lineID, 0);
						}
						return clipObj;
					}
			}
			
			return null;
		}


		/**
		 * <summary>Gets the lipsync file associated with a speech line</summary>
		 * <param name = "lineID">The ID number of the speech line, as generated by the Speech Manager</param>
		 * <param name = "_speaker">The character speaking the line</param>
		 * <returns>Gets the lipsync file associated with a speech line</returns> 
		 */
		public virtual T GetSpeechLipsyncFile <T> (int lineID, Char _speaker) where T : Object
		{
			if (!KickStarter.speechManager.IsTextTypeTranslatable (AC_TextType.Speech))
			{
				return null;
			}

			int voiceLanguage = Options.GetVoiceLanguage ();
			string voiceLanguageName = (voiceLanguage > 0) ? Options.GetVoiceLanguageName () : string.Empty;

			switch (KickStarter.speechManager.referenceSpeechFiles)
			{
				case ReferenceSpeechFiles.ByNamingConvention:
					{
						string fullName = KickStarter.speechManager.GetAutoAssetPathAndName (lineID, _speaker, voiceLanguageName, true);

						T lipsyncFile = Resources.Load (fullName) as T;

						if (lipsyncFile == null && KickStarter.speechManager.fallbackAudio && voiceLanguage > 0)
						{
							fullName = KickStarter.speechManager.GetAutoAssetPathAndName (lineID, _speaker, string.Empty, true);
							lipsyncFile = Resources.Load (fullName) as T;
						}

						if (lipsyncFile == null)
						{
							ACDebug.LogWarning ("Lipsync file 'Resources/" + fullName + "' (" + typeof (T) + ") not found.");
						}
						return lipsyncFile;
					}

				case ReferenceSpeechFiles.ByAssetBundle:
					{
						string fullName = KickStarter.speechManager.GetAutoAssetPathAndName (lineID, _speaker, voiceLanguageName, true);

						if (isLoadingBundle)
						{
							ACDebug.LogWarning ("Cannot load lipsync file from AssetBundle as the AssetBundle is still being loaded.");
							return null;
						}

						int indexOfLastSlash = fullName.LastIndexOf ("/") + 1;
						if (indexOfLastSlash > 0)
						{
							fullName = fullName.Substring (indexOfLastSlash);
						}

						if (currentLipsyncAssetBundle == null)
						{
							ACDebug.LogWarning ("Cannot load lipsync file '" + fullName + "' from AssetBundle as no AssetBundle is currently loaded.");
							return null;
						}


						T lipsyncFile = currentLipsyncAssetBundle.LoadAsset<T> (fullName);

						if (lipsyncFile == null && !string.IsNullOrEmpty (fullName))
						{
							ACDebug.LogWarning ("Lipsync file '" + fullName + "' (" + typeof (T) + ") not found in Asset Bundle '" + currentLipsyncAssetBundle.name + "'.");
						}
						return lipsyncFile;
					}

				case ReferenceSpeechFiles.ByDirectReference:
					{
						Object _object = KickStarter.runtimeLanguages.GetLineCustomLipsyncFile (lineID, voiceLanguage);

						if (_object == null && KickStarter.speechManager.fallbackAudio && voiceLanguage > 0)
						{
							_object = KickStarter.runtimeLanguages.GetLineCustomLipsyncFile (lineID, 0);
						}

						if (_object is T)
						{
							return (T) KickStarter.runtimeLanguages.GetLineCustomLipsyncFile (lineID, voiceLanguage);
						}
					}
					break;

				default:
					break;
			}

			return null;
		}


		/**
		 * <summary>Gets the translation of a line of text, based on the game's current language.</summary>
		 * <param name = "lineID">The ITranslatable instance's line ID.</param>
		 * <returns>The translatable text.</returns>
		 */
		public string GetTranslation (int lineID)
		{
			if (lineID >= 0)
			{
				SpeechLine speechLine;
				if (SpeechLinesDictionary.TryGetValue (lineID, out speechLine))
				{
					return GetTranslation (speechLine.text, lineID, Options.GetLanguage ());
				}
				ACDebug.LogWarning ("No translation for line ID " + lineID + " could be found");
			}
			return string.Empty;
		}


#if LocalizationIsPresent && AddressableIsPresent

		public void ExtractSpeechMetadata (LocalizedString localizedString, System.Action<SpeechMetadata, AudioClip, TextAsset> callback)
		{
			StartCoroutine (ExtractSpeechMetadataCo (localizedString, callback));
		}
		

		private IEnumerator ExtractSpeechMetadataCo (LocalizedString localizedString, System.Action<SpeechMetadata, AudioClip, TextAsset> callback)
		{
			var entry = LocalizationSettings.StringDatabase.GetTableEntryAsync (localizedString.TableReference, localizedString.TableEntryReference);
			while (!entry.IsDone)
			{
				yield return null;
			}
			var result = entry.Result;

			var stringTableEntry = result.Entry;
			
			if (stringTableEntry == null)
			{
				callback?.Invoke (null, null, null);
				yield break;
			}

			var metadata = stringTableEntry.GetMetadata<SpeechMetadata> ();
			if (metadata == null)
			{
				metadata = stringTableEntry.SharedEntry.Metadata.GetMetadata<SpeechMetadata> ();
			}

			AudioClip audioClip = null;
			TextAsset lipsyncData = null;

			if (metadata != null && metadata.AudioClipReference != null && metadata.AudioClipReference.RuntimeKeyIsValid ())
			{
				var audioHandle = metadata.AudioClipReference.LoadAssetAsync<AudioClip>();
				yield return audioHandle;
				if (audioHandle.Status == AsyncOperationStatus.Succeeded && audioHandle.Result != null)
				{
					audioClip = audioHandle.Result;
				}
			}

			if (metadata != null && metadata.LipSyncDataReference != null && metadata.LipSyncDataReference.RuntimeKeyIsValid ())
			{
				var lipSyncHandle = metadata.LipSyncDataReference.LoadAssetAsync<TextAsset>();
				yield return lipSyncHandle;
				if (lipSyncHandle.Status == AsyncOperationStatus.Succeeded && lipSyncHandle.Result != null)
				{
					lipsyncData = lipSyncHandle.Result;
				}
			}

			callback?.Invoke (metadata, audioClip, lipsyncData);
		}

#endif


		/**
		 * <summary>Gets the translation of a line of text.</summary>
		 * <param name = "originalText">The line in its original language.</param>
		 * <param name = "_lineID">The translation ID number generated by SpeechManager's PopulateList() function</param>
		 * <param name = "language">The index number of the language to return the line in, where 0 = the game's original language.</param>
		 * <returns>The translation of the line, if it exists. If a translation does not exist, then the original line will be returned.</returns>
		 */
		public string GetTranslation (string originalText, int _lineID, int language)
		{
			if (string.IsNullOrEmpty (originalText))
			{
				return string.Empty;
			}

			if (language == 0 && _lineID == -1)
			{
				return originalText;
			}
			
			if (_lineID == -1 || language < 0)
			{
				return originalText;
			}
			else
			{
				if (SpeechLinesDictionary.TryGetValue (_lineID, out speechLine))
				{
					#if LocalizationIsPresent
					if (speechLine.useLocalizedString)
					{
						return speechLine.localizedString.GetLocalizedString ();
					}
					else
					#endif
					if (language == 0)
					{
						return originalText;
					}

					if (speechLine.translationText.Count > (language - 1))
					{
						string result = speechLine.translationText[language - 1];
						if (string.IsNullOrEmpty (result))
						{
							int fallbackLanguageIndex = Languages[language].fallbackLanguageIndex;
							if (fallbackLanguageIndex > 0 && fallbackLanguageIndex <= Languages.Count)
							{
								result = speechLine.translationText[fallbackLanguageIndex - 1];
							}
							else
							{
								result = originalText;
							}
						}
							return result;
					}
					else
					{
						ACDebug.LogWarning ("A translation is being requested that does not exist!");
					}
				}
				else
				{
					if (language == 0)
					{
						return originalText;
					}
					
					if (KickStarter.settingsManager.showDebugLogs != ShowDebugLogs.Never)
					{
						SpeechLine originalLine = KickStarter.speechManager.GetLine (_lineID);
						if (originalLine == null)
						{
							ACDebug.LogWarning ("Cannot find translation for '" + originalText + "' because it's Line ID (" + _lineID + ") was not found in the Speech Manager.");
						}
						else
						{
							ACDebug.LogWarning ("Cannot find translation for '" + originalText + "' (line ID = " + _lineID + ")");
						}
					}
 					return originalText;
				}
			}

			return string.Empty;
		}


		/**
		 * <summary>Gets the translation of a line of text.</summary>
		 * <param name = "originalText">The line in its original language.</param>
		 * <param name = "_lineID">The translation ID number generated by SpeechManager's PopulateList() function</param>
		 * <param name = "language">The index number of the language to return the line in, where 0 = the game's original language.</param>
		 * <param name = "textType">The type of text to translatable.</param>
		 * <returns>The translation of the line, if it exists. If a translation does not exist, or the given textType is not translatable, then the original line will be returned.</returns>
		 */
		public string GetTranslation (string originalText, int _lineID, int language, AC_TextType textType)
		{
			if (KickStarter.speechManager == null || KickStarter.speechManager.IsTextTypeTranslatable (textType))
			{
				return GetTranslation (originalText, _lineID, language);
			}
			return originalText;
		}


		/**
		 * <summary>Gets the translation data for a line of text.</summary>
		 * <param name = "originalText">The line in its original language.</param>
		 * <param name = "_lineID">The translation ID number generated by SpeechManager's PopulateList() function</param>
		 * <param name = "language">The index number of the language to return the line in, where 0 = the game's original language.</param>
		 * <param name = "textType">The type of text to translatable.</param>
		 * <returns>The translation data of the line, if it exists. If a translation does not exist, or the given textType is not translatable, then null will be returned.</returns>
		 */
		public SpeechLine GetSpeechLine (string originalText, int _lineID, int language, AC_TextType textType)
		{
			if (KickStarter.speechManager == null || KickStarter.speechManager.IsTextTypeTranslatable (textType))
			{
				if (language == 0 || string.IsNullOrEmpty (originalText))
				{
					return null;
				}

				if (_lineID == -1 || language <= 0)
				{
					ACDebug.Log ("Cannot find translation for '" + originalText + "' because the text has not been added to the Speech Manager.");
					return null;
				}
				else
				{
					SpeechLine speechLine;
					if (SpeechLinesDictionary.TryGetValue (_lineID, out speechLine))
					{
						return speechLine;
					}
				}
			}
			return null;
		}


		/**
		 * <summary>Gets a line of text, translated (if applicable) to the current language.</summary>
		 * <param name = "_lineID">The translation ID number generated by SpeechManager's PopulateList() function</param>
		 * <returns>A line of text, translated (if applicable) to the current language.</returnsy>
		 */
		public string GetCurrentLanguageText (int _lineID)
		{
			int language = Options.GetLanguage ();

			if (_lineID < 0 || language < 0)
			{
				return string.Empty;
			}
			else
			{
				SpeechLine speechLine;
				if (SpeechLinesDictionary.TryGetValue (_lineID, out speechLine))
				{
					if (language == 0)
					{
						return speechLine.text;
					}

					if (speechLine.translationText.Count > (language-1))
					{
						return speechLine.translationText [language-1];
					}
					else
					{
						ACDebug.LogWarning ("A translation is being requested that does not exist!");
					}
				}
				else
				{
					ACDebug.LogWarning ("Cannot find translation for line ID " + _lineID + " because it was not found in the Speech Manager.");
				}
			}

			return string.Empty;
		}


		/**
		 * <summary>Gets all translations of a line of text.</summary>
		 * <param name = "_lineID">The translation ID number generated by SpeechManager's PopulateList() function</param>
		 * <returns>All translations of the line, if they exist. If a translation does not exist, nothing will be returned.</returns>
		 */
		public string[] GetTranslations (int _lineID)
		{
			if (_lineID == -1)
			{
				return null;
			}
			else
			{
				SpeechLine speechLine;
				if (SpeechLinesDictionary.TryGetValue (_lineID, out speechLine))
				{
					return speechLine.translationText.ToArray ();
				}
			}
			return null;
		}


		/**
		 * <summary>Updates the translation of a given line for a given language.</summary>
		 * <param name = "lineID">The ID of the text to update, as generated by the Speech Manager</param>
		 * <param name = "languageIndex">The index number of the language to update.  Must be greater than 0, since 0 is the game's original language</param>
		 * <param name = "translationText">The updated translation text</param>
		 */
		public void UpdateRuntimeTranslation (int lineID, int languageIndex, string translationText)
		{
			if (languageIndex <= 0)
			{
				ACDebug.LogWarning ("The language index must be greater than zero.");
			}

			SpeechLine speechLine;
			if (SpeechLinesDictionary.TryGetValue (lineID, out speechLine))
			{
				speechLine.translationText [languageIndex-1] = translationText;
			}
		}


		/**
		 * <summary>Gets the text of an ITranslatable instance, based on the game's current language.</summary>
		 * <param name = "translatable">The ITranslatable instance.</param>
		 * <param name = "index">The index of the ITranslatable's array of translatable text</param>
		 * <returns>The translatable text.</returns>
		 */
		public string GetTranslatableText (ITranslatable translatable, int index = 0)
		{
			int language = Options.GetLanguage ();
			string originalText = translatable.GetTranslatableString (index);
			int lineID = translatable.GetTranslationID (index);

			return GetTranslation (originalText, lineID, language);
		}


		/**
		 * <summary>Imports a translation CSV file (as generated by the Speech Manager) into the game - either as a new language, or as an update to an existing one. The first column MUST be the ID number of each line, and the first row must be for the header.</summary>
		 * <param name = "textAsset">The CSV file as a text asset.</param>
		 * <param name = "languageName">The name of the language.  If a language by this name already exists in the system, the import process will update it.</param>
		 * <param name = "newTextColumn">The column number (starting from zero) that holds the new translation.  This must be greater than zero, as the first column should be occupied by the ID numbers.</param>
		 * <param name = "ignoreEmptyCells">If True, then empty cells will not be imported and the original language will be used instead</param>
		 * <param name = "isRTL">If True, the language is read right-to-left</summary>
		 */
		public void ImportRuntimeTranslation (TextAsset textAsset, string languageName, int newTextColumn, bool ignoreEmptyCells = false, bool isRTL = false)
		{
			if (textAsset != null && !string.IsNullOrEmpty (textAsset.text))
			{
				if (newTextColumn <= 0)
				{
					ACDebug.LogWarning ("Error importing language from " + textAsset.name + " - newTextColumn must be greater than zero, as the first column is reserved for ID numbers.");
					return;
				}

				int existingIndex = GetLanguageIndex (languageName);
				if (existingIndex >= 0)
				{
					int i = existingIndex;
					languages[i].isRightToLeft = isRTL;
					ProcessTranslationFile (i, textAsset.text, newTextColumn, ignoreEmptyCells);
					ACDebug.Log ("Updated language " + languageName);
				}
				else
				{
					CreateLanguage (languageName, isRTL);
					int i = languages.Count - 1;
					ProcessTranslationFile (i, textAsset.text, newTextColumn, ignoreEmptyCells);
					ACDebug.Log ("Created new language " + languageName);
				}
			}
		}


		/**
		 * <summary>Checks if a given language reads right-to-left, Hebrew/Arabic-style</summary>
		 * <param name = "languageIndex">The index number of the language to check, where 0 is the game's original language</param>
		 * <returns>True if the given language reads right-to-left</returns>
		 */
		public bool LanguageReadsRightToLeft (int languageIndex)
		{
			if (languageIndex >= 0 && languageIndex < languages.Count)
			{
				return languages[languageIndex].isRightToLeft;
			}
			return false;
		}


		/**
		 * <summary>Checks if a given language reads right-to-left, Hebrew/Arabic-style</summary>
		 * <param name = "languageName">The name of the language to check, as written in the Speech Manager</param>
		 * <returns>True if the given language reads right-to-left</returns>
		 */
		public bool LanguageReadsRightToLeft (string languageName)
		{
			int index = LanguageNameToIndex (languageName);
			return LanguageReadsRightToLeft (index);
		}


		/**
		 * <summary>Marks a speech line as having been spoken, so that it cannot be spoken again.  This will only work for speech lines that have 'Can only play once?' checked in their Speech Manager entry.</summary>
		 * <param name = "lineID">The line being spoken</param>
		 * <returns>True if the line can be spoken, False if it has already been spoken and cannot be spoken again.</returns>
		 */
		public bool MarkLineAsSpoken (int lineID)
		{
			if (lineID < 0)
			{
				return true;
			}

			if (spokenOnceSpeechLineIDs.Contains (lineID))
			{
				return false;
			}

			SpeechLine speechLine;
			if (SpeechLinesDictionary.TryGetValue (lineID, out speechLine))
			{
				if (speechLine.onlyPlaySpeechOnce)
				{
					spokenOnceSpeechLineIDs.Add (lineID);
				}
			}

			return true;
		}


		/**
		 * <summary>Updates a MainData class with its own variables that need saving.</summary>
		 * <param name = "mainData">The original MainData class</param>
		 * <returns>The updated MainData class</returns>
		 */
		public MainData SaveMainData (MainData mainData)
		{
			System.Text.StringBuilder spokenLinesData = new System.Text.StringBuilder ();

			for (int i=0; i<spokenOnceSpeechLineIDs.Count; i++)
			{
				spokenLinesData.Append (spokenOnceSpeechLineIDs[i].ToString ());
				spokenLinesData.Append (SaveSystem.colon);
			}

			if (spokenOnceSpeechLineIDs.Count > 0)
			{
				spokenLinesData.Remove (spokenLinesData.Length-1, 1);
			}

			mainData.spokenLinesData = spokenLinesData.ToString ();
			return mainData;
		}


		/**
		 * <summary>Updates its own variables from a MainData class.</summary>
		 * <param name = "mainData">The MainData class to load from</param>
		 */
		public void LoadMainData (MainData mainData)
		{
			spokenOnceSpeechLineIDs.Clear ();

			string spokenLinesData = mainData.spokenLinesData;
			if (!string.IsNullOrEmpty (spokenLinesData))
			{
				string[] linesArray = spokenLinesData.Split (SaveSystem.colon[0]);

				foreach (string chunk in linesArray)
				{
					int _id = -1;
					if (int.TryParse (chunk, out _id) && _id >= 0)
					{
						spokenOnceSpeechLineIDs.Add (_id);
					}
				}
			}
		}


		public int TrueLanguageIndexToEnabledIndex (int trueIndex)
		{
			int enabledIndex = -1;

			for (int i = 0; i <= trueIndex; i++)
			{
				if (!Languages[i].isDisabled)
				{
					enabledIndex++;
				}
			}

			return enabledIndex;
		}


		public int GetEnabledLanguageIndex (int trueIndex)
		{
			if (trueIndex == 0 && Languages[0].isDisabled && Languages.Count > 1)
			{
				if (!Languages[1].isDisabled)
				{
					return 1;
				}
				trueIndex = 1;
			}

			if (trueIndex > 0 && trueIndex < Languages.Count)
			{
				if (Languages[trueIndex].isDisabled)
				{
					return Languages[trueIndex].fallbackLanguageIndex;
				}
				return trueIndex;
			}
			return 0;
		}


		public int EnabledLanguageToTrueIndex (int enabledIndex)
		{
			int correctedIndex = -1;

			for (int i = 0; i <= Languages.Count; i++)
			{
				if (!Languages[i].isDisabled)
				{
					correctedIndex++;
				}

				if (enabledIndex == correctedIndex)
				{
					return i;
				}
			}

			ACDebug.LogWarning ("Could not convert enabled language index " + enabledIndex + " to true index");
			return 0;
		}


		public int GetNumEnabledLanguages ()
		{
			int numEnabledLanguages = 0;

			for (int i = 0; i < Languages.Count; i++)
			{
				if (!Languages[i].isDisabled)
				{
					numEnabledLanguages++;
				}
			}

			return numEnabledLanguages;
		}

		
		public void CallOnSetLanguageEvent (int language)
		{
			#if LocalizationIsPresent
			if (KickStarter.speechManager.autoSyncLocaleWithLanguage)
			{
				if (setLocaleCoroutine != null)
				{
					StopCoroutine (setLocaleCoroutine);
				}
				setLocaleCoroutine = StartCoroutine (SetLocaleCo (language));
			}
			#else
			KickStarter.eventManager.Call_OnChangeLanguage (language);
			#endif
		}

		#endregion


		#region ProtectedFunctions

		protected int LanguageNameToIndex (string languageName)
		{
			if (!string.IsNullOrEmpty (languageName))
			{
				for (int i = 0; i < languages.Count; i++)
				{
					if (languages[i].name == languageName)
					{
						return i;
					}
				}
			}
			return -1;
		}


		protected void TransferFromManager ()
		{
			if (KickStarter.speechManager)
			{
				SpeechManager speechManager = KickStarter.speechManager;
				speechManager.Upgrade ();

				languages.Clear ();
				bool anyIsEnabled = false;
				foreach (Language _language in speechManager.Languages)
				{
					Language copiedLanguage = new Language (_language);
					if (!copiedLanguage.isDisabled)
					{
						anyIsEnabled = true;
					}
					languages.Add (copiedLanguage);
				}

				if (!anyIsEnabled && languages.Count > 0)
				{
					ACDebug.LogWarning ("At least one language must be enabled - enabling the original");
					languages[0].isDisabled = false;
				}
			}
		}


		protected IEnumerator LoadAssetBundleCoroutine (int i)
		{
			isLoadingBundle = true;

			if (!KickStarter.speechManager.translateAudio)
			{
				i = 0;
			}

			if (currentAudioAssetBundleName != languages[i].audioAssetBundle &&
				currentLipsyncAssetBundleName != languages[i].audioAssetBundle)
			{
				if (!string.IsNullOrEmpty (languages[i].audioAssetBundle))
				{
					string bundlePath = Path.Combine (Application.streamingAssetsPath, languages[i].audioAssetBundle);
					var bundleLoadRequest = AssetBundle.LoadFromFileAsync (bundlePath);

					yield return bundleLoadRequest;

					CurrentAudioAssetBundle = bundleLoadRequest.assetBundle;

					if (currentAudioAssetBundle == null)
					{
						ACDebug.LogWarning("Failed to load AssetBundle '" + bundlePath + "'");
					}
					else
					{
						currentAudioAssetBundleName = languages[i].audioAssetBundle;
					}
				}
				else
				{
					// None found
					CurrentAudioAssetBundle = null;
					currentAudioAssetBundleName = string.Empty;
				}
			}

			if (KickStarter.speechManager.UseFileBasedLipSyncing ())
			{
				if (currentLipsyncAssetBundleName != languages[i].lipsyncAssetBundle)
				{
					if (!string.IsNullOrEmpty (languages[i].lipsyncAssetBundle))
					{
						if (currentAudioAssetBundleName == languages[i].lipsyncAssetBundle)
						{
							CurrentLipsyncAssetBundle = currentAudioAssetBundle;
							currentLipsyncAssetBundleName = currentAudioAssetBundleName;
						}
						else
						{
							string bundlePath = Path.Combine (Application.streamingAssetsPath, languages[i].lipsyncAssetBundle);
							var bundleLoadRequest = AssetBundle.LoadFromFileAsync (bundlePath);
							
			        		yield return bundleLoadRequest;

							CurrentLipsyncAssetBundle = bundleLoadRequest.assetBundle;
							if (currentLipsyncAssetBundle == null)
							{
								ACDebug.LogWarning ("Failed to load AssetBundle '" + bundlePath + "'");
							}
							else
							{
								currentLipsyncAssetBundleName = languages[i].lipsyncAssetBundle;
							}
						}
					}
					else
					{
						// None found
						CurrentLipsyncAssetBundle = null;
						currentLipsyncAssetBundleName = string.Empty;
					}
				}
			}

			isLoadingBundle = false;

			KickStarter.eventManager.Call_OnLoadSpeechAssetBundle (i);
		}


		protected AudioClip GetLineCustomAudioClip (int _lineID, int _language = 0)
		{
			SpeechLine speechLine;
			if (SpeechLinesDictionary.TryGetValue (_lineID, out speechLine))
			{
				if (KickStarter.speechManager.translateAudio && _language > 0)
				{
					if (speechLine.customTranslationAudioClips != null && speechLine.customTranslationAudioClips.Count > (_language - 1))
					{
						return speechLine.customTranslationAudioClips [_language - 1];
					}
				}
				else
				{
					return speechLine.customAudioClip;
				}
			}
			return null;
		}


		protected UnityEngine.Object GetLineCustomLipsyncFile (int _lineID, int _language = 0)
		{
			SpeechLine speechLine;
			if (SpeechLinesDictionary.TryGetValue (_lineID, out speechLine))
			{
				if (KickStarter.speechManager.translateAudio && _language > 0)
				{
					if (speechLine.customTranslationLipsyncFiles != null && speechLine.customTranslationLipsyncFiles.Count > (_language - 1))
					{
						return speechLine.customTranslationLipsyncFiles [_language - 1];
					}
				}
				else
				{
					return speechLine.customLipsyncFile;
				}
			}
			return null;
		}


		protected void CreateLanguage (string name, bool isRTL)
		{
			languages.Add (new Language (name, isRTL));

			foreach (SpeechLine speechManagerLine in KickStarter.speechManager.lines)
			{
				int _lineID = speechManagerLine.lineID;

				SpeechLine speechLine = null;
				if (SpeechLinesDictionary.TryGetValue (_lineID, out speechLine))
				{
					speechLine.translationText.Add (speechLine.text);
					continue;
				}
			}
		}
		
		
		protected void ProcessTranslationFile (int i, string csvText, int newTextColumn, bool ignoreEmptyCells)
		{
			string [,] csvOutput = CSVReader.SplitCsvGrid (csvText);
			
			int lineID = 0;
			string translationText = string.Empty;

			if (csvOutput.GetLength (0) <= newTextColumn)
			{
				ACDebug.LogWarning ("Cannot import translation file, as it does not have enough columns - searching for column index " + newTextColumn);
				return;
			}

			for (int y = 1; y < csvOutput.GetLength (1); y++)
			{
				if (csvOutput [0,y] != null && csvOutput [0,y].Length > 0)
				{
					lineID = -1;
					if (int.TryParse (csvOutput [0,y], out lineID))
					{
						translationText = csvOutput [newTextColumn, y];
						translationText = AddLineBreaks (translationText);

						if (!ignoreEmptyCells || !string.IsNullOrEmpty (translationText))
						{
							UpdateRuntimeTranslation (lineID, i, translationText);
						}
					}
					else
					{
						ACDebug.LogWarning ("Error importing translation (ID:" + csvOutput [0,y] + ") on row #" + y.ToString () + ".");
					}
				}
			}
		}


		protected string AddLineBreaks (string text)
		{
			if (!string.IsNullOrEmpty (text))
			{
				return (text.Replace ("[break]", "\n"));
			}
			return string.Empty;
		}


		protected int GetLanguageIndex (string languageName)
		{
			for (int i = 0; i < Languages.Count; i++)
			{
				if (Languages[i].name == languageName)
				{
					return i;
				}
			}
			return -1;
		}
	
		#endregion


		#if LocalizationIsPresent

		private bool initLocaleSettings;
		private Coroutine setLocaleCoroutine;

		private IEnumerator SetLocaleCo (int index)
		{
			if (!initLocaleSettings)
			{
				yield return LocalizationSettings.InitializationOperation;
				initLocaleSettings = true;
			}

			if (index <  LocalizationSettings.AvailableLocales.Locales.Count)
			{
				LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[index];
			}
			else
			{
				ACDebug.LogWarning ("Cannot sync AC language with Locale because index " + index + " cannot be found");
			}

			KickStarter.eventManager.Call_OnChangeLanguage (index);
		}

		#endif


		#region GetSet

		/** The AssetBundle to retrieve audio files from */
		public AssetBundle CurrentAudioAssetBundle
		{
			get
			{
				return currentAudioAssetBundle;
			}
			set
			{
				if (currentAudioAssetBundle && currentAudioAssetBundle != value)
				{
					currentAudioAssetBundle.Unload (true);
				}

				currentAudioAssetBundle = value;
			}
		}


		/** The AssetBundle to retrieve lipsync files from */
		public AssetBundle CurrentLipsyncAssetBundle
		{
			get
			{
				return currentLipsyncAssetBundle;
			}
			set
			{
				if (currentLipsyncAssetBundle && currentLipsyncAssetBundle != value)
				{
					currentLipsyncAssetBundle.Unload (true);
				}

				currentLipsyncAssetBundle = value;
			}
		}


		/** The game's languages. The first is always "Original". */
		public List<Language> Languages
		{
			get
			{
				return languages;
			}
		}


		/** True if an audio or lipsync asset bundle is currently being loaded into memory */
		public bool IsLoadingBundle
		{
			get
			{
				return isLoadingBundle;
			}
		}


		private Dictionary<int, SpeechLine> SpeechLinesDictionary
		{
			get
			{
				if (speechLinesDictionary == null)
				{
					speechLinesDictionary = new Dictionary<int, SpeechLine> ();
					speechLinesDictionary.Clear ();
					foreach (SpeechLine speechLine in KickStarter.speechManager.lines)
					{
						if (KickStarter.speechManager.IsTextTypeTranslatable (speechLine.textType))
						{
							speechLinesDictionary.Add (speechLine.lineID, new SpeechLine (speechLine, Options.GetVoiceLanguage ()));
						}
					}
				}
				return speechLinesDictionary;
			}
		}

		#endregion

	}

}