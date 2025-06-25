using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GPG315.TaylorSprankling
{
    public class AssetFinder : EditorWindow
    {
        private const string PluginName = "Scene Asset Finder";
        private readonly string[] _scenesToCheckToolbarString = { "Current", "Selected", "Build List", "Project" };
        
        private Vector2 _scrollPosition;
        private int _scenesToCheckToolbarInt = 3;
        private bool _showScenesChecked;
        private bool _ignorePackagesFolder = true;
        private bool _ignoreEditorFolders = true;
        
        private static List<SceneAsset> _sceneAssets;
        private static List<string> _dependencyPaths;
        private static List<Object> _filteredDependencies;
        private static List<string> _unusedAssetPaths;
        private static List<Object> _filteredUnusedAssets;
        
        
        [MenuItem("Window/GPG315 Plugins/" + PluginName)]
        public static void ShowWindow()
        {
            GetWindow<AssetFinder>(PluginName);
        }
        
        [MenuItem("Assets/" + PluginName + "/Add to used assets", false)]
        public static void AddToAssets()
        {
            foreach (Object obj in Selection.objects)
            {
                string objPath = AssetDatabase.GetAssetPath(obj);
                if (!_dependencyPaths.Contains(objPath))
                {
                    _dependencyPaths.Add(objPath);
                }
                if (_unusedAssetPaths.Contains(objPath))
                {
                    _unusedAssetPaths.Remove(objPath);
                }
            }
            string s = Selection.objects.Length != 1 ? "s" : "";
            Debug.Log($"{Selection.objects.Length} selected asset{s} marked as dependant");
        }
        
        [MenuItem("Assets/" + PluginName + "/Add to used assets", true)]
        public static bool AddToAssets(MenuCommand menuCommand)
        {
            return _sceneAssets != null;
        }
        
        [MenuItem("Assets/" + PluginName + "/Remove from used assets", false)]
        public static void RemoveFromAssets()
        {
            foreach (Object obj in Selection.objects)
            {
                string objPath = AssetDatabase.GetAssetPath(obj);
                if (_dependencyPaths.Contains(objPath))
                {
                    _dependencyPaths.Remove(objPath);
                }
                if (!_unusedAssetPaths.Contains(objPath))
                {
                    _unusedAssetPaths.Add(objPath);
                }
            }
            string s = Selection.objects.Length != 1 ? "s" : "";
            Debug.Log($"{Selection.objects.Length} selected asset{s} removed from dependencies");
        }
        
        [MenuItem("Assets/" + PluginName + "/Remove from used assets", true)]
        public static bool RemoveFromAssets(MenuCommand menuCommand)
        {
            return _sceneAssets != null;
        }
        
        private void OnGUI()
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Scenes to scan:", EditorStyles.boldLabel);
            _scenesToCheckToolbarInt = GUILayout.Toolbar(_scenesToCheckToolbarInt, _scenesToCheckToolbarString);
            GUILayout.EndHorizontal();
            
            string scenesToCheckInfoBox = _scenesToCheckToolbarInt switch
            {
                0 => "The current open scene will be checked",
                1 => "Only the selected scenes will be checked",
                2 => "All scenes within the build list will be checked",
                3 => "All scenes throughout whole project will be checked",
                4 => "Selection choice included no scenes...",
                _ => "WHAT THE HECK IS HAPPENING HERE"
            };
            GUILayout.Box(scenesToCheckInfoBox);
            
            if (GUILayout.Button("Scan for associated assets"))
            {
                ClearData();
                
                switch (_scenesToCheckToolbarInt)
                {
                    case 0:
                        for (int i = 0; i < SceneManager.sceneCount; i++)
                        {
                            _sceneAssets ??= new List<SceneAsset>();
                            _sceneAssets.Add(AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneManager.GetSceneAt(i).path));
                        }
                        if (_sceneAssets != null) break;
                        _scenesToCheckToolbarInt = 4;
                        break;
                    
                    case 1:
                        if (Selection.assetGUIDs.Length > 0)
                        {
                            AddSceneAssetFromGUIDs(Selection.assetGUIDs);
                            if (_sceneAssets != null) break;
                        }
                        _scenesToCheckToolbarInt = 4;
                        break;
                    
                    case 2:
                        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                        {
                            _sceneAssets ??= new List<SceneAsset>();
                            _sceneAssets.Add(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path));
                        }
                        if (_sceneAssets != null) break;
                        _scenesToCheckToolbarInt = 4;
                        break;
                    
                    case 3:
                        AddSceneAssetFromGUIDs(AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets" }));
                        if (_sceneAssets != null) break; 
                        _scenesToCheckToolbarInt = 4; 
                        break;
                }
                
                if (_sceneAssets != null)
                {
                    string[] sceneAssetPaths = new string[_sceneAssets.Count];
                    for (int i = 0; i < _sceneAssets.Count; i++)
                    {
                        sceneAssetPaths[i] = AssetDatabase.GetAssetPath(_sceneAssets[i]);
                    }
                    
                    _dependencyPaths = AssetDatabase.GetDependencies(sceneAssetPaths).ToList();
                    
                    SelectDependencies();
                    
                    _showScenesChecked = _sceneAssets.Count <= 10;
                }
                
            }
            
            if (_sceneAssets != null)
            {
                string s = _sceneAssets.Count != 1 ? "s" : "";
                _showScenesChecked = EditorGUILayout.BeginFoldoutHeaderGroup(_showScenesChecked, $"{_sceneAssets.Count} scene{s} scanned", EditorStyles.foldoutHeader);
                if (_showScenesChecked)
                {
                    for (int index = 0; index < _sceneAssets.Count; index++)
                    {
                        GUILayout.Space(-2);
                        if (GUILayout.Button($"{_sceneAssets[index].name}", EditorStyles.linkLabel))
                        {
                            Selection.activeObject = null;
                            Selection.activeObject = _sceneAssets[index];
                        }
                        if (index >= 99) break;
                    }
                    
                    if (_sceneAssets.Count > 100)
                    {
                        GUILayout.Space(-2);
                        GUILayout.Label($"+ {_sceneAssets.Count - 100} more scenes");
                    }
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
                
                GUILayout.Space(10);
                
                string ss = _dependencyPaths.Count != 1 ? "s" : "";
                GUILayout.Label($"{_dependencyPaths.Count} associated asset{ss} found");
                GUILayout.Space(-2);
                GUILayout.Label("*Includes unfiltered selections", EditorStyles.miniLabel);
                
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Select all associated assets"))
                {
                    SelectDependencies();
                }
                
                if (GUILayout.Button("Select all unused assets"))
                {
                    _filteredUnusedAssets = new List<Object>();
                    
                    if (_unusedAssetPaths == null)
                    {
                        _unusedAssetPaths = new List<string>();
                        string[] allAssetGUIDs = AssetDatabase.FindAssets(string.Empty);
                        foreach (string assetGUID in allAssetGUIDs)
                        {
                            string assetPath = AssetDatabase.GUIDToAssetPath(assetGUID);
                            
                            if (!_dependencyPaths.Contains(assetPath))
                            {
                                if (assetPath.StartsWith("Packages/")) continue; // Always ignore the packages folder
                                Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                                if (asset is DefaultAsset) continue; // Always ignore default types
                                _unusedAssetPaths.Add(assetPath);
                                if (_ignoreEditorFolders && assetPath.Contains("/Editor/")) continue; // Ignore editor folders
                                _filteredUnusedAssets.Add(asset);
                            }
                        }
                    }
                    else
                    {
                        foreach (string assetPath in _unusedAssetPaths)
                        {
                            if (_ignoreEditorFolders && assetPath.Contains("/Editor/")) continue; // Ignore editor folders
                            _filteredUnusedAssets.Add(AssetDatabase.LoadAssetAtPath<Object>(assetPath));
                        }
                    }
                    
                    Selection.objects = _filteredUnusedAssets.ToArray();
                }
                
                if (GUILayout.Button("Clear"))
                {
                    ClearData();
                }
                GUILayout.EndHorizontal();
            }
            
            GUILayout.Space(25);
            
            GUILayout.Label("Options", EditorStyles.boldLabel);
            _ignorePackagesFolder = GUILayout.Toggle(_ignorePackagesFolder, "Ignore Packages folder");
            _ignoreEditorFolders = GUILayout.Toggle(_ignoreEditorFolders, "Ignore Editor folders");
            
            if (_sceneAssets != null)
            {
                GUILayout.Space(10);
                GUILayout.Box("Packages folder is always ignored when selecting unused assets");
            }
            
            GUILayout.EndScrollView();
        }
        
        private void AddSceneAssetFromGUIDs(string[] assetGUIDs)
        {
            foreach (string assetGUID in assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(assetGUID);
                SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (!asset) continue;
                _sceneAssets ??= new List<SceneAsset>();
                _sceneAssets.Add(asset);
            }
        }
        
        private void SelectDependencies()
        {
            _filteredDependencies = new List<Object>();
            foreach (string dependencyPath in _dependencyPaths)
            {
                if (_ignorePackagesFolder && dependencyPath.StartsWith("Packages")) continue; // Ignore the packages folder
                if (_ignoreEditorFolders && dependencyPath.Contains("/Editor/")) continue; // Ignore editor folders
                _filteredDependencies.Add(AssetDatabase.LoadAssetAtPath<Object>(dependencyPath));
            }
            Selection.objects = _filteredDependencies.ToArray();
        }
        
        private static void ClearData()
        {
            _sceneAssets = null;
            _dependencyPaths = null;
            _filteredDependencies = null;
            _unusedAssetPaths = null;
            _filteredUnusedAssets = null;
        }
    }
}