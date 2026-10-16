using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DeliverySystem))]
public class DeliverySystemEditor : Editor
{
    private static readonly string[] ConversationFields =
    {
        "pickupConversation", "startedConversation", "startedReturnConversation",
        "finishedConversation", "arrivalConversation", "deliveredConversation",
        "droppedCargoConversation", "nextDeliveryConversation"
    };

    private static readonly string[] ConversationLabels =
    {
        "Mission Start", "Package Loaded", "Return While Active", "Finished",
        "Arrival", "Delivered", "Dropped Cargo", "Next Delivery"
    };

    private static readonly string[] LaterConversationLabels =
    {
        "Return While Active", "Finished", "Arrival", "Delivered", "Dropped Cargo", "Next Delivery"
    };

    private static readonly string[] FinalConversationLabels =
    {
        "Mission Start", "Package Loaded", "Return While Active", "Finished",
        "Arrival", "Delivered", "Dropped Cargo"
    };

    private static readonly string[] LaterFinalConversationLabels =
    {
        "Return While Active", "Finished", "Arrival", "Delivered", "Dropped Cargo"
    };

    private static readonly string[] RouteFields =
    {
        "id", "objectiveName", "giverVoice", "cargoPrefab", "safeCargoAnchor", "cargoSpawnHeight",
        "droppedDistance", "destroyCargoOnDelivery",
        "completionDestination", "completionTeleportDelay", "completionGroundClearance",
        "completionSun", "completionDirectionalLightToDisable", "completionFogColor", "completionAmbientColor",
        "completionVehiclePrefab", "completionCamera"
    };

    private int selectedRoute;
    private int selectedConversation;
    private int selectedLine;
    private int selectedCompletionLine;
    private bool showRouteSettings;
    private bool showEvents;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty routes = serializedObject.FindProperty("deliveries");
        if (routes == null)
        {
            EditorGUILayout.HelpBox("Delivery routes could not be found.", MessageType.Error);
            return;
        }

        if (routes.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No delivery routes are configured.", MessageType.Info);
            if (GUILayout.Button("Add Empty Route")) AddEmptyRoute(routes);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        selectedRoute = Mathf.Clamp(selectedRoute, 0, routes.arraySize - 1);
        DrawRouteNavigation(routes);
        SerializedProperty route = routes.GetArrayElementAtIndex(selectedRoute);
        SerializedProperty dialogueFont = serializedObject.FindProperty("dialogueFont");
        if (dialogueFont != null) EditorGUILayout.PropertyField(dialogueFont);
        SerializedProperty restartFadeSeconds = serializedObject.FindProperty("restartFadeSeconds");
        if (restartFadeSeconds != null) EditorGUILayout.PropertyField(restartFadeSeconds);
        EditorGUILayout.Space(4);

        showRouteSettings = EditorGUILayout.Foldout(showRouteSettings, "Route Settings", true);
        if (showRouteSettings)
        {
            EditorGUI.indentLevel++;
            foreach (string name in RouteFields)
            {
                SerializedProperty field = route.FindPropertyRelative(name);
                if (field != null) EditorGUILayout.PropertyField(field);
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(8);
        bool finalRoute = selectedRoute == routes.arraySize - 1;
        if (selectedRoute == 0)
        {
            if (finalRoute) selectedConversation = Mathf.Min(selectedConversation, 6);
            selectedConversation = EditorGUILayout.Popup("Conversation", selectedConversation,
                finalRoute ? FinalConversationLabels : ConversationLabels);
        }
        else
        {
            // Later jobs are accepted by the previous route's Next Delivery conversation.
            // Keep their old serialized start lines intact, but do not show unused fields.
            selectedConversation = Mathf.Clamp(selectedConversation, 2, finalRoute ? 6 : 7);
            selectedConversation = 2 + EditorGUILayout.Popup(
                "Conversation", selectedConversation - 2,
                finalRoute ? LaterFinalConversationLabels : LaterConversationLabels);
        }
        SerializedProperty lines = route.FindPropertyRelative(ConversationFields[selectedConversation]);
        if (lines == null)
        {
            EditorGUILayout.HelpBox("This conversation could not be found.", MessageType.Error);
        }
        else
        {
            DrawConversation(lines, ref selectedLine);
        }

        SerializedProperty completionDestination = route.FindPropertyRelative("completionDestination");
        SerializedProperty completionConversation = route.FindPropertyRelative("completionConversation");
        if (completionDestination != null && completionDestination.objectReferenceValue != null && completionConversation != null)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Completion Viewpoint Conversation", EditorStyles.boldLabel);
            DrawConversation(completionConversation, ref selectedCompletionLine);
        }

        DeliverySystem deliverySystem = (DeliverySystem)target;
        using (new EditorGUI.DisabledScope(!Application.isPlaying || completionDestination == null || completionDestination.objectReferenceValue == null))
        {
            if (GUILayout.Button("Test Completion Viewpoint"))
            {
                serializedObject.ApplyModifiedProperties();
                deliverySystem.DebugCompletionViewpoint(route.FindPropertyRelative("id").stringValue);
            }
        }

        showEvents = EditorGUILayout.Foldout(showEvents, "Events", true);
        if (showEvents)
        {
            EditorGUI.indentLevel++;
            DrawField(route, "onStarted");
            DrawField(route, "onCargoDropped");
            DrawField(route, "onFinished");
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawRouteNavigation(SerializedProperty routes)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(selectedRoute == 0))
            {
                if (GUILayout.Button("Previous", GUILayout.Width(75)))
                    SelectRoute(selectedRoute - 1);
            }

            SerializedProperty route = routes.GetArrayElementAtIndex(selectedRoute);
            string name = route.FindPropertyRelative("objectiveName").stringValue;
            GUILayout.Label($"{selectedRoute + 1}/{routes.arraySize}  {name}", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(selectedRoute >= routes.arraySize - 1))
            {
                if (GUILayout.Button("Next", GUILayout.Width(55)))
                    SelectRoute(selectedRoute + 1);
            }
        }

        if (GUILayout.Button("Add Empty Route")) AddEmptyRoute(routes);
    }

    private void DrawConversation(SerializedProperty lines, ref int lineIndex)
    {
        int count = lines.arraySize;
        if (count == 0)
        {
            EditorGUILayout.LabelField("No dialogue lines yet.");
            if (GUILayout.Button("Add Dialogue Line")) AddLine(lines, ref lineIndex);
            return;
        }

        lineIndex = Mathf.Clamp(lineIndex, 0, count - 1);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(lineIndex == 0))
            {
                if (GUILayout.Button("Previous", GUILayout.Width(75))) lineIndex--;
            }
            lineIndex = EditorGUILayout.IntSlider("Line", lineIndex + 1, 1, count) - 1;
            using (new EditorGUI.DisabledScope(lineIndex >= count - 1))
            {
                if (GUILayout.Button("Next", GUILayout.Width(55))) lineIndex++;
            }
        }

        SerializedProperty line = lines.GetArrayElementAtIndex(lineIndex);
        DrawField(line, "speaker");
        SerializedProperty dialogueText = line.FindPropertyRelative("text");
        if (dialogueText != null)
        {
            EditorGUILayout.LabelField("Text");
            EditorGUI.BeginChangeCheck();
            string editedText = EditorGUILayout.TextArea(
                dialogueText.stringValue, GUILayout.MinHeight(70));
            if (EditorGUI.EndChangeCheck()) dialogueText.stringValue = editedText;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add After")) AddLine(lines, ref lineIndex);
            using (new EditorGUI.DisabledScope(lineIndex == 0))
            {
                if (GUILayout.Button("Move Up"))
                {
                    lines.MoveArrayElement(lineIndex, lineIndex - 1);
                    lineIndex--;
                }
            }
            using (new EditorGUI.DisabledScope(lineIndex >= count - 1))
            {
                if (GUILayout.Button("Move Down"))
                {
                    lines.MoveArrayElement(lineIndex, lineIndex + 1);
                    lineIndex++;
                }
            }
            if (GUILayout.Button("Remove")) RemoveLine(lines, ref lineIndex);
        }
    }

    private void SelectRoute(int index)
    {
        selectedRoute = index;
        selectedConversation = index == 0 ? 0 : 4;
        selectedLine = 0;
    }

    private void AddLine(SerializedProperty lines, ref int lineIndex)
    {
        int index = lines.arraySize == 0 ? 0 : lineIndex + 1;
        lines.InsertArrayElementAtIndex(index);
        SerializedProperty line = lines.GetArrayElementAtIndex(index);
        line.FindPropertyRelative("speaker").enumValueIndex = 0;
        line.FindPropertyRelative("text").stringValue = string.Empty;
        lineIndex = index;
    }

    private void RemoveLine(SerializedProperty lines, ref int lineIndex)
    {
        if (!EditorUtility.DisplayDialog("Remove dialogue line?",
                $"Remove line {lineIndex + 1} from this conversation?", "Remove", "Cancel"))
            return;

        lines.DeleteArrayElementAtIndex(lineIndex);
        lineIndex = Mathf.Clamp(lineIndex, 0, lines.arraySize - 1);
    }

    private static void DrawField(SerializedProperty parent, string name)
    {
        SerializedProperty field = parent.FindPropertyRelative(name);
        if (field != null) EditorGUILayout.PropertyField(field);
    }

    private void AddEmptyRoute(SerializedProperty routes)
    {
        int index = routes.arraySize;
        routes.arraySize++;
        SerializedProperty route = routes.GetArrayElementAtIndex(index);

        route.FindPropertyRelative("id").stringValue = $"NewDelivery_{index + 1}";
        route.FindPropertyRelative("objectiveName").stringValue = "New delivery";
        route.FindPropertyRelative("giverVoice").enumValueIndex = 0;
        route.FindPropertyRelative("cargoPrefab").objectReferenceValue = null;
        route.FindPropertyRelative("safeCargoAnchor").objectReferenceValue = null;
        route.FindPropertyRelative("cargoSpawnHeight").floatValue = 1f;
        route.FindPropertyRelative("droppedDistance").floatValue = 3f;
        route.FindPropertyRelative("destroyCargoOnDelivery").boolValue = true;
        route.FindPropertyRelative("acceptAfterNotStartedDialogue").boolValue = true;
        route.FindPropertyRelative("completionDestination").objectReferenceValue = null;
        route.FindPropertyRelative("completionTeleportDelay").floatValue = .75f;
        route.FindPropertyRelative("completionGroundClearance").floatValue = .35f;
        route.FindPropertyRelative("completionConversation").arraySize = 0;
        route.FindPropertyRelative("completionSun").objectReferenceValue = null;
        route.FindPropertyRelative("completionDirectionalLightToDisable").objectReferenceValue = null;
        route.FindPropertyRelative("completionFogColor").colorValue = new Color(.48f, .58f, .68f, 1f);
        route.FindPropertyRelative("completionAmbientColor").colorValue = new Color(.48f, .58f, .68f, 1f);
        route.FindPropertyRelative("completionVehiclePrefab").objectReferenceValue = null;
        route.FindPropertyRelative("completionCamera").objectReferenceValue = null;

        foreach (string field in ConversationFields)
            route.FindPropertyRelative(field).arraySize = 0;

        foreach (string eventName in new[] { "onStarted", "onCargoDropped", "onFinished" })
        {
            SerializedProperty persistentCalls = route.FindPropertyRelative(eventName)
                .FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (persistentCalls != null) persistentCalls.arraySize = 0;
        }

        selectedRoute = index;
        selectedConversation = index == 0 ? 0 : 4;
        selectedLine = 0;
    }
}
