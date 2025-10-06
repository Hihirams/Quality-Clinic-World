#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Inspector custom para AreaBootstrapper con botón "Configure Now".
/// </summary>
[CustomEditor(typeof(AreaBootstrapper))]
public class AreaConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var bootstrapper = (AreaBootstrapper)target;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Area Bootstrapper", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("config"));

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Detección de Geometría", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("autoDetectChildCubes"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("areasLayerName"));

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Colliders", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("perChildBoxColliders"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("parentEnclosingCollider"));

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("AreaCard", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ensureAreaCard"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cardHeightOffset"));

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Label (ManualAreaLabel)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ensureManualLabel"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("labelObjectPrefix"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("labelHeightY"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("labelSortingOrder"));

        EditorGUILayout.Space(10);

        // Validaciones simples
        if (bootstrapper.config == null)
        {
            EditorGUILayout.HelpBox("Asigna un AreaConfigSO antes de configurar.", MessageType.Warning);
        }
        else
        {
            if (string.IsNullOrEmpty(bootstrapper.config.areaKey))
                EditorGUILayout.HelpBox("El AreaConfigSO no tiene areaKey.", MessageType.Warning);
        }

        EditorGUI.BeginDisabledGroup(bootstrapper.config == null);
        if (GUILayout.Button("Configure Now", GUILayout.Height(34)))
        {
            foreach (var obj in targets)
            {
                var bs = (AreaBootstrapper)obj;
                Undo.RegisterFullObjectHierarchyUndo(bs.gameObject, "Area Configure");
                bs.ConfigureNow();
                EditorUtility.SetDirty(bs);
            }
        }
        EditorGUI.EndDisabledGroup();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
