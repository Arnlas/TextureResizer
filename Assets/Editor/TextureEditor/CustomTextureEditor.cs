using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor;
using UnityEngine;

namespace TextureEditor
{
    public enum TextureResolution
    {
        ThirtyTwo = 32,
        SixtyFour = 64,
        OneTwentyEight = 128,
        TwoFiftySix = 256,
        FiveTwelve = 512,
        TenTwentyFour = 1024,
        TwentyFortyEight = 2048,
        FortyNinetySix = 4096,
        EightyOneNinetyTwo = 8192
    }

    public class CustomTextureEditor : EditorWindow
    {
        #region Constants and Readonly

        private const string InitialSearchPath = "Assets";
        private static readonly string[] AccessiblePlatforms = {"Default", "Standalone", "Nintendo Switch"};
        private readonly int[] _platformIds = {0, 1, 2};
        private readonly string[] _resolutionNames = {"32", "64", "128", "256", "512", "1024", "2048", "4096", "8192"};
        private readonly int[] _resolutions = {32, 64, 128, 256, 512, 1024, 2048, 4096, 8192};

        #endregion

        #region Public Static Vars

        public static TextureResolution SearchResolution = TextureResolution.FortyNinetySix;
        public static TextureResolution TargetResolution = TextureResolution.TwentyFortyEight;
        public static bool EnablePlatformOverride = false;
        public static int PlatformId = 0;
        public static string SearchPath = "Assets";

        #endregion

        #region Private Variables

        private bool _scanWholeProject = true;
        private bool _advanced = false;
        private string _searchPath;
        private int _scanResolution = 4096;
        private int _rewriteResolution = 2048;
        private List<TextureImporter> _textureImporters = new List<TextureImporter>();
        private List<TextureImporter> _forceEnablePlatformOverride = new List<TextureImporter>();
        private int _searchPlatform = 0;

        #endregion

        public static void SwitchTexturesResolution()
        {
            if (!System.IO.Directory.Exists(SearchPath))
            {
                Debug.LogError("No Such Path or Directory");
                return;
            }
            
            if (PlatformId >= AccessiblePlatforms.Length)
            {
                Debug.LogError("Out of Platforms Range");
                return;
            }
            
            string platform = AccessiblePlatforms[PlatformId];
            
            List<TextureImporter> textureImporters = FindTextureImporters(SearchPath);
            List<TextureImporter> forcePlatformOverrides = new List<TextureImporter>();
            FilterTextures(textureImporters, forcePlatformOverrides, (int) SearchResolution, platform);

            int count = textureImporters.Count;
            if (count == 0)
            {
                Debug.LogError($"Directory contains {count} textures with demand Resolution");
                return;
            }

            Debug.LogError($"Discovered {count} Acceptable Textures");

            foreach (TextureImporter tI in textureImporters)
            {
                OverrideTextureSettings(tI, (int) TargetResolution, platform);
            }
            
            if (!EnablePlatformOverride) goto skipPoint;
            
            foreach (TextureImporter tI in forcePlatformOverrides)
            {
                ForceEnableOverrides(tI, platform);
            }

            skipPoint:
            Debug.LogError("Done!!!");
        }

        //Unity Editor Windows Draw method.
        [MenuItem("Window/Custom/TextureEditor")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(CustomTextureEditor));
        }
        
        #region Private Methods
        private void OnGUI()
        {
            _searchPlatform = EditorGUILayout.IntPopup("Platform to Override", _searchPlatform,
                AccessiblePlatforms, _platformIds);
            
            GuiLine();
            Scan();
            Rewrite();
        }
        private void Scan()
        {
            GUILayout.Label("Texture Scan Settings");
            GUILayout.Space(10);
            
            _scanWholeProject = EditorGUILayout.Toggle("Scan Settings", _scanWholeProject);
            
            
            if (_scanWholeProject)
            {
                EditorGUILayout.LabelField("You are about to search entire Assets folder. It might take a while!!!");
                _searchPath = InitialSearchPath;
            }
            else
            {
                _searchPath = EditorGUILayout.TextField("Search Path", _searchPath);
            }

            _scanResolution =
                EditorGUILayout.IntPopup("Texture Search Param", _scanResolution, _resolutionNames, _resolutions);
            
            if (GUILayout.Button("Scan"))
            {
                if (!System.IO.Directory.Exists(_searchPath))
                {
                    Debug.LogError("No Such File or Directory");
                    return;
                }
                
                _textureImporters = FindTextureImporters(_searchPath);
                FilterTextures(_textureImporters, _forceEnablePlatformOverride, _scanResolution, AccessiblePlatforms[_searchPlatform]);
                Debug.LogError(_textureImporters.Count);
            }
        }
        private void Rewrite()
        {
            GuiLine();
            GUILayout.Label("Texture Rewrite Settings");
            GUILayout.Space(10);
            
            _rewriteResolution =
                EditorGUILayout.IntPopup("Texture Search Param", _rewriteResolution, _resolutionNames, _resolutions);
            
            if (GUILayout.Button("Override") && _textureImporters.Count > 0)
            {
                foreach (TextureImporter tI in _textureImporters)
                {
                    OverrideTextureSettings(tI, _rewriteResolution, AccessiblePlatforms[_searchPlatform]);
                }
                
                _forceEnablePlatformOverride.Clear();
            }

            _advanced = EditorGUILayout.Toggle("Advanced Scan Options", _advanced);
            
            if (_advanced && GUILayout.Button("Override and Enable Platform"))
            {
                foreach (TextureImporter tI in _textureImporters)
                {
                    OverrideTextureSettings(tI, _rewriteResolution, AccessiblePlatforms[_searchPlatform]);
                }

                foreach (TextureImporter tI in _forceEnablePlatformOverride)
                {
                    ForceEnableOverrides(tI, AccessiblePlatforms[_searchPlatform]);
                }
                
                _forceEnablePlatformOverride.Clear();
            }
        }

        private static void OverrideTextureSettings(TextureImporter tI, int resolution, string platform)
        {
            TextureImporterPlatformSettings tS = null;
            if (platform == "Default")
            {
                tI.maxTextureSize = resolution;
            }
            else
            {
                tS = tI.GetPlatformTextureSettings(platform);

                if (tS != null)
                {
                    tS.overridden = true;
                    tS.maxTextureSize = resolution;
                    tI.SetPlatformTextureSettings(tS);
                }
            }

            string path = AssetDatabase.GetAssetPath(tI);
            AssetDatabase.WriteImportSettingsIfDirty (path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void ForceEnableOverrides(TextureImporter tI, string platform)
        {
            TextureImporterPlatformSettings tS = null;
            if (platform == "Default") return;
            
            tS = tI.GetPlatformTextureSettings(platform);

            if (tS != null)
            {
                tS.overridden = true;
                tI.SetPlatformTextureSettings(tS);
            }
            
            string path = AssetDatabase.GetAssetPath(tI);
            AssetDatabase.WriteImportSettingsIfDirty (path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        
        private void GuiLine( int height = 1, int spaceHeight = 10)
        {
            GUILayout.Space(spaceHeight);
            Rect rect = EditorGUILayout.GetControlRect(false, height );

            rect.height = height;
            EditorGUI.DrawRect(rect, new Color ( 0.5f,0.5f,0.5f, 1 ) );
            GUILayout.Space(spaceHeight);
        }
        private static void FilterTextures(List<TextureImporter> textures, List<TextureImporter> forceOverride, int scanResolution, string platform)
        {
            for (int i = textures.Count - 1; i >= 0; i--)
            {
                if (platform == "Default")
                {
                    if (textures[i].maxTextureSize != scanResolution) textures.RemoveAt(i);
                }
                else
                {
                    TextureImporterPlatformSettings tI = textures[i].GetPlatformTextureSettings(platform);

                    if (tI.maxTextureSize != scanResolution)
                    {
                        if (!tI.overridden) forceOverride.Add(textures[i]);
                        textures.RemoveAt(i);
                    }
                }
            }
        }
        private static List<TextureImporter> FindTextureImporters(string path)
        {
            List<TextureImporter> assets = new List<TextureImporter>();
            string[] guids = AssetDatabase.FindAssets("t:texture2D", new[] {path});
            
            for( int i = 0; i < guids.Length; i++ )
            {
                string assetPath = AssetDatabase.GUIDToAssetPath( guids[i] );
                TextureImporter asset = TextureImporter.GetAtPath(assetPath) as TextureImporter;
                if( asset != null )
                {
                    assets.Add(asset);
                }
            }
            return assets;
        }
        #endregion
    }
}
