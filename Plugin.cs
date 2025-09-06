using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using BepInEx.Unity.Mono.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using TeamCherry.Localization;
using TeamCherry.SharedUtils;
using UnityEngine;
using UnityEngine.UI;

namespace SilkSong_CustomLang
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.yourname.languageeditor";
        public const string PluginName = "Language Editor";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource logger;
        internal static ConfigEntry<KeyboardShortcut> DumpKey;
        internal static ConfigEntry<KeyboardShortcut> ReloadKey;

        internal static string exportPath;
        internal static string importPath;

        internal static Dictionary<string, string> modifiedLanguageFiles = new Dictionary<string, string>();

        public void Awake()
        {
            logger = Logger;

            try
            {
                // 配置按键绑定
                DumpKey = Config.Bind("Hotkeys", "Dump Languages",
                    new KeyboardShortcut(KeyCode.F9),
                    "Press to dump all language files");
                ReloadKey = Config.Bind("Hotkeys", "Reload Languages",
                    new KeyboardShortcut(KeyCode.F10),
                    "" +
                    "Reload language file");

                // 设置导出和导入路径
                exportPath = Path.Combine(Paths.BepInExRootPath, "LanguageExport");
                importPath = Path.Combine(Paths.BepInExRootPath, "LanguageImport");

                // 确保目录存在
                Directory.CreateDirectory(exportPath);
                Directory.CreateDirectory(importPath);

                LoadModifiedLanguageFiles();

                Harmony.CreateAndPatchAll(typeof(Plugin));

                logger.LogInfo("Language Editor plugin loaded");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error loading plugin: {ex}");
            }
        }

        private void Update()
        {
            // 检测按键
            if (DumpKey.Value.IsDown())
            {
                DumpAllLanguages();
            }
            if (ReloadKey.Value.IsDown())
            {
                IWillFuckingManualChangeTheLanguage();
            }
        }

        private void LoadModifiedLanguageFiles()
        {
            string importPath = Path.Combine(Paths.BepInExRootPath, "LanguageImport");

            if (!Directory.Exists(importPath))
            {
                logger.LogWarning($"LanguageImport directory not found: {importPath}");
                return;
            }

            // 遍历所有语言目录
            foreach (string langDir in Directory.GetDirectories(importPath))
            {
                string langCode = Path.GetFileName(langDir);

                // 遍历所有XML文件
                foreach (string xmlFile in Directory.GetFiles(langDir, "*.xml"))
                {
                    string sheetName = Path.GetFileNameWithoutExtension(xmlFile);
                    string resourceKey = $"{langCode}_{sheetName}";

                    try
                    {
                        // 读取并加密修改后的内容
                        string modifiedContent = File.ReadAllText(xmlFile);
                        string encryptedContent = Encryption.Encrypt(modifiedContent);

                        modifiedLanguageFiles[resourceKey] = encryptedContent;
                        logger.LogInfo($"Loaded modified language file: {resourceKey}");
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"Error loading {xmlFile}: {e}");
                    }
                }
            }
        }

        // 导出所有语言文件
        public static void DumpAllLanguages()
        {
            try
            {
                // 获取可用语言列表
                var availableLanguages = GetAvailableLanguages();
                var sheetTitles = GetSheetTitles();

                logger.LogInfo($"Found {availableLanguages.Count} languages and {sheetTitles.Length} sheets");

                foreach (var langCode in availableLanguages)
                {
                    foreach (var sheetTitle in sheetTitles)
                    {
                        DumpLanguageFile(langCode, sheetTitle);
                    }
                }

                logger.LogInfo($"Language files dumped to: {exportPath}");
            }
            catch (Exception e)
            {
                logger.LogError($"Error dumping languages: {e}");
            }
        }

        // 导出单个语言文件
        private static void DumpLanguageFile(string langCode, string sheetTitle)
        {
            try
            {
                // 获取原始加密内容
                TextAsset textAsset = (TextAsset)Resources.Load($"Languages/{langCode}_{sheetTitle}", typeof(TextAsset));
                if (textAsset == null)
                {
                    logger.LogWarning($"Language file not found: {langCode}_{sheetTitle}");
                    return;
                }

                // 解密内容
                string decryptedContent = Encryption.Decrypt(textAsset.text);

                // 保存到文件
                string langDir = Path.Combine(exportPath, langCode);
                Directory.CreateDirectory(langDir);

                string filePath = Path.Combine(langDir, $"{sheetTitle}.xml");
                File.WriteAllText(filePath, decryptedContent);

                logger.LogInfo($"Exported: {langCode}/{sheetTitle}.xml");
            }
            catch (Exception e)
            {
                logger.LogError($"Error dumping {langCode}_{sheetTitle}: {e}");
            }
        }
        // 使用反射获取可用语言
        private static List<string> GetAvailableLanguages()
        {
            FieldInfo availableLanguagesField = typeof(Language).GetField("_availableLanguages",
                BindingFlags.NonPublic | BindingFlags.Static);

            if (availableLanguagesField != null)
            {
                return (List<string>)availableLanguagesField.GetValue(null) ?? [];
            }

            return [];
        }

        // 使用反射获取工作表标题
        private static string[] GetSheetTitles()
        {
            PropertyInfo settingsProperty = typeof(Language).GetProperty("Settings",
                BindingFlags.NonPublic | BindingFlags.Static);

            if (settingsProperty != null)
            {
                LocalizationSettings settings = (LocalizationSettings)settingsProperty.GetValue(null);
                return settings?.sheetTitles ?? new string[0];
            }

            return new string[0];
        }
        public static string GetCurrentLanguageCode()
        {
            try
            {
                FieldInfo currentLanguageField = typeof(Language).GetField("_currentLanguage",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (currentLanguageField != null)
                {
                    LanguageCode currentLanguage = (LanguageCode)currentLanguageField.GetValue(null);
                    return currentLanguage.ToString();
                }
            }
            catch (Exception e)
            {
                Plugin.logger.LogError($"Error getting current language: {e}");
            }

            return "CN"; // 默认中文
        }
        public static void IWillFuckingManualChangeTheLanguage()
        {
            try
            {
                // 获取 _settings 字段
                FieldInfo settingsField = typeof(Language).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Static);
                if (settingsField == null)
                {
                    Plugin.logger.LogError("Failed to find _settings field");
                    return;
                }

                LocalizationSettings settings = (LocalizationSettings)settingsField.GetValue(null);
                if (settings.sheetTitles == null)
                {
                    Plugin.logger.LogError("Failed to get settings or sheetTitles");
                    return;
                }

                // 获取 _currentEntrySheets 字段
                FieldInfo currentEntrySheetsField = typeof(Language).GetField("_currentEntrySheets", BindingFlags.NonPublic | BindingFlags.Static);
                if (currentEntrySheetsField == null)
                {
                    Plugin.logger.LogError("Failed to find _currentEntrySheets field");
                    return;
                }

                // 获取当前条目表
                var currentEntrySheets = (Dictionary<string, Dictionary<string, string>>)currentEntrySheetsField.GetValue(null);
                if (currentEntrySheets == null)
                {
                    Plugin.logger.LogError("Current entry sheets is null");
                    return;
                }

                // 不要重新创建字典，而是直接修改现有的字典
                // currentEntrySheets = new Dictionary<string, Dictionary<string, string>>(); // 这行是错误的

                foreach (string sheetTitle in settings.sheetTitles)
                {
                    // 检查是否有自定义文件
                    string customFilePath = Path.Combine(Plugin.importPath, Language.CurrentLanguage().ToString(), $"{sheetTitle}.xml");

                    if (File.Exists(customFilePath))
                    {
                        Plugin.logger.LogInfo($"Loading custom language file: {customFilePath}");

                        // 读取自定义文件内容
                        string customContent = File.ReadAllText(customFilePath);

                        // 解析XML并更新条目表
                        using (XmlReader xmlReader = XmlReader.Create(new StringReader(customContent)))
                        {
                            while (xmlReader.ReadToFollowing("entry"))
                            {
                                xmlReader.MoveToFirstAttribute();
                                string key = xmlReader.Value;
                                xmlReader.MoveToElement();
                                string value = xmlReader.ReadElementContentAsString().Trim();
                                value = value.UnescapeXml();

                                // 确保工作表存在
                                if (!currentEntrySheets.ContainsKey(sheetTitle))
                                {
                                    currentEntrySheets[sheetTitle] = new Dictionary<string, string>();
                                }

                                // 更新或添加条目
                                currentEntrySheets[sheetTitle][key] = value;
                            }
                        }
                    }
                }

                // 通过反射设置回修改后的字典（虽然可能不需要，因为引用类型）
                currentEntrySheetsField.SetValue(null, currentEntrySheets);

                // 刷新本地化
                Type gameManagerType = Type.GetType("GameManager, Assembly-CSharp");
                if (gameManagerType != null)
                {
                    PropertyInfo instanceProperty = gameManagerType.GetProperty("instance");
                    if (instanceProperty != null)
                    {
                        object gameManagerInstance = instanceProperty.GetValue(null);
                        if (gameManagerInstance != null)
                        {
                            MethodInfo refreshMethod = gameManagerType.GetMethod("RefreshLocalization");
                            if (refreshMethod != null)
                            {
                                refreshMethod.Invoke(gameManagerInstance, null);
                                Plugin.logger.LogInfo("RefreshLocalization called successfully");
                            }
                            else
                            {
                                Plugin.logger.LogError("RefreshLocalization method not found");
                            }
                        }
                        else
                        {
                            Plugin.logger.LogError("GameManager instance is null");
                        }
                    }
                    else
                    {
                        Plugin.logger.LogError("instance property not found");
                    }
                }
                else
                {
                    Plugin.logger.LogError("GameManager type not found");
                }

                Plugin.logger.LogInfo("Language changed successfully");
            }
            catch (Exception e)
            {
                Plugin.logger.LogError($"Error in IWillFuckingManualChangeTheLanguage: {e}");
            }
        }
    }
}