using System;
using System.Collections.Generic;
using GGJ2025.Utils;
using UnityEngine;

namespace GGJ2025.Models
{
    [Serializable]
    public class ConversationOptionData
    {
        public string type;
        public string objectKey;
        public string answer;
        public string[] validItems;
    }

    [Serializable]
    public class ConversationEntry
    {
        public string question;
        public string answer;
    }

    [Serializable]
    public class JsonConversationData
    {
        public ConversationEntry[] conversations;
        public string question_structure;
        public string response_structure;
        public ResponseData responses;
        public ActionData actions;
    }

    [Serializable]
    public class ResponseData
    {
        public string[] acknowledgements;
        public OutcomeDictionary outcomes;
    }

    [Serializable]
    public class OutcomeValue
    {
        public string[] left;
        public string[] left_items;
        public string[] right;
        public string[] right_items;
        public string[] speculations;
    }

    [Serializable]
    public class ActionData
    {
        public Dictionary<string, ActionItemData> objects;
        public Dictionary<string, ActionItemData> npcs;
        public Dictionary<string, ActionItemData> locations;
    }

    [Serializable]
    public class ActionItemData
    {
        public string name;
        public string[] actions;
        public string[] valid_results;
    }

    [Serializable]
	public enum ConversationStage
	{
		Question,
		Response,
		Action,
		Simple_Answer
	}
	

    [Serializable]
    public class ObjectData
    {
        public string[] observations;
        public string[] causes;
        public string[] mystical_questions;
        public string[] actions;
    }

    [Serializable]
    public class OutcomeDictionary
    {
        public Dictionary<string, OutcomeValue> items = new Dictionary<string, OutcomeValue>();

        public static OutcomeDictionary CreateFromJSON(string inputJson)
        {
            try
            {
                var wrapper = new OutcomeDictionary();
                
                // Remove ONLY newlines and carriage returns, NOT spaces
                string cleanJson = inputJson
                    .Replace("\n", " ")
                    .Replace("\r", " ");
                
                int outcomesStart = cleanJson.IndexOf("\"outcomes\":");
                if (outcomesStart == -1) return wrapper;
                
                outcomesStart = cleanJson.IndexOf("{", outcomesStart);
                if (outcomesStart == -1) return wrapper;
                
                int outcomesEnd = JsonParser.FindMatchingBrace(cleanJson, outcomesStart);
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
                    
                    int valueEnd = JsonParser.FindMatchingBrace(outcomesJson, valueStart);
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

    [Serializable]
    public class ObjectDictionary
    {
        public Dictionary<string, ObjectData> items = new Dictionary<string, ObjectData>();

        public static ObjectDictionary CreateFromJSON(string inputJson)
        {
            try
            {
                var wrapper = new ObjectDictionary();
                
                // Remove ONLY newlines and carriage returns, NOT spaces
                string cleanJson = inputJson
                    .Replace("\n", " ")
                    .Replace("\r", " ");
                // Note: We're no longer removing spaces
                
                int objectsStart = cleanJson.IndexOf("\"objects\":");
                if (objectsStart == -1) return wrapper;
                
                objectsStart = cleanJson.IndexOf("{", objectsStart);
                if (objectsStart == -1) return wrapper;
                
                int objectsEnd = JsonParser.FindMatchingBrace(cleanJson, objectsStart);
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
                    
                    int valueEnd = JsonParser.FindMatchingBrace(objectsJson, valueStart);
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

    public static class ConversationTracker
    {
        private static HashSet<int> globalUsedIndices = new HashSet<int>();
        private static bool hasUsedAllConversations = false;

        public static void MarkConversationUsed(int index)
        {
            globalUsedIndices.Add(index);
        }

        public static bool IsConversationUsed(int index)
        {
            return globalUsedIndices.Contains(index);
        }

        public static void ResetTracking()
        {
            globalUsedIndices.Clear();
            hasUsedAllConversations = false;
        }

        public static bool HasUsedAllConversations
        {
            get => hasUsedAllConversations;
            set => hasUsedAllConversations = value;
        }
    }
} 