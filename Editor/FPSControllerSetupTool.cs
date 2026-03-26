using UnityEngine;
using UnityEditor;

namespace Mandible.FPS
{
    public class FPSControllerSetupTool : EditorWindow
    { 
        //Application
        static string iconPath = "Packages/com.unity.dt.app-ui/PackageResources/Icons/Regular/FocusModeTracking.png";
        static string defaultIcon = "d_PreMatCube";

        //Refs
        private GameObject target;

        [MenuItem("Mandible/FPS Controller/Setup Tool", false, priority = 1005)]
        public static void ShowWindow()
        {
            FPSControllerSetupTool window = GetWindow<FPSControllerSetupTool>("FPS Controller Setup Tool");
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (icon == null) 
                icon = EditorGUIUtility.IconContent(defaultIcon).image as Texture2D;

            window.titleContent = new GUIContent("FPS Controller Setup Tool", icon);

            window.target = Selection.activeGameObject;

            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("FPS Controller Setup Tool", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("This tool lets you setup a generic/humanoid rigged character with an FPS Controller.");

            EditorGUILayout.Space();

            //Initial Reference
            target = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Target", "Target GameObject to setup as an Entity"),
                target,
                typeof(GameObject),
                true
            );

        }
    }
}
