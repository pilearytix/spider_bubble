using System;
using System.Linq;
using UnityEngine;
using GGJ2025.Models;

namespace GGJ2025.Utils


{
    public static class JsonParser
    {
        public static string[] ParseStringArray(string jsonString, string arrayName)
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

        public static int FindMatchingBrace(string text, int openBracePos)
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
    }
} 