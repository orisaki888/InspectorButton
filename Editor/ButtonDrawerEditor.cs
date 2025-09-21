using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;
using System.Collections;
using System;

#if INSPECTOR_BUTTON_ALL_TARGETS
[CustomEditor(typeof(UnityEngine.Object), true, isFallback = true)]
#else
[CustomEditor(typeof(MonoBehaviour), true, isFallback = true)]
#endif
[CanEditMultipleObjects]
public class ButtonDrawerEditor : Editor
{
    private const string FoldoutPrefKeyPrefix = "InspectorButton_Foldout_";
    private class MethodButton
    {
        public MethodInfo Method { get; }
        public string ButtonText { get; }
        public ParameterInfo[] Parameters { get; }
        public object[] ParameterValues { get; set; }
        public bool HasUnsupportedParameters { get; private set; }

        public MethodButton(MethodInfo method, ButtonAttribute attribute)
        {
            Method = method;
            ButtonText = string.IsNullOrEmpty(attribute.ButtonName) ? ObjectNames.NicifyVariableName(method.Name) : attribute.ButtonName;
            Parameters = method.GetParameters();
            ParameterValues = new object[Parameters.Length];
            HasUnsupportedParameters = false;

            for (int i = 0; i < Parameters.Length; i++)
            {
                if (!IsTypeSupported(Parameters[i].ParameterType))
                {
                    HasUnsupportedParameters = true;
                    Debug.LogWarning($"Method '{method.Name}' has an unsupported parameter type: '{Parameters[i].ParameterType.Name}'. Default value will be used if possible.");
                }
                // パラメータのデフォルト値または型のデフォルト値を設定
                ParameterValues[i] = GetDefaultValue(Parameters[i]);
            }
        }

        private object GetDefaultValue(ParameterInfo paramInfo)
        {
            if (paramInfo.HasDefaultValue)
            {
                return paramInfo.DefaultValue;
            }
            Type paramType = paramInfo.ParameterType;
            if (paramType.IsValueType)
            {
                try { return Activator.CreateInstance(paramType); }
                catch { return null; } // 引数なしコンストラクタがない場合など
            }
            return null; // 参照型はnull
        }
    }

    private List<MethodButton> _methodButtons;
    private Dictionary<string, bool> _foldoutStates = new Dictionary<string, bool>();

    private void OnEnable()
    {
        _methodButtons = new List<MethodButton>();
        if (target == null) return;

        var targetType = target.GetType();
        var methods = targetType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            var buttonAttribute = method.GetCustomAttribute<ButtonAttribute>();
            if (buttonAttribute != null)
            {
                _methodButtons.Add(new MethodButton(method, buttonAttribute));
            }
        }
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI(); // 通常のインスペクターを描画

        if (_methodButtons == null || _methodButtons.Count == 0)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Custom Buttons", EditorStyles.boldLabel);
        foreach (var mb in _methodButtons)
        {
            if (mb.HasUnsupportedParameters)
            {
                 EditorGUILayout.HelpBox($"Button '{mb.ButtonText}' has unsupported parameter types. Execution might fail or use default values.", MessageType.Warning);
            }

            if (mb.Parameters.Length > 0)
            {
                string foldoutKey = $"{FoldoutPrefKeyPrefix}{target.GetInstanceID()}_{mb.Method.DeclaringType?.FullName}.{mb.Method.Name}";
                if (!_foldoutStates.ContainsKey(foldoutKey))
                {
                    _foldoutStates[foldoutKey] = EditorPrefs.GetBool(foldoutKey, false); // Foldout状態を記憶
                }

                bool currentFoldoutState = EditorGUILayout.Foldout(_foldoutStates[foldoutKey], mb.ButtonText, true, EditorStyles.foldoutHeader);
                if (currentFoldoutState != _foldoutStates[foldoutKey])
                {
                    _foldoutStates[foldoutKey] = currentFoldoutState;
                    EditorPrefs.SetBool(foldoutKey, currentFoldoutState); // 状態を保存
                }


                if (currentFoldoutState)
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < mb.Parameters.Length; i++)
                    {
                        var param = mb.Parameters[i];
                        mb.ParameterValues[i] = DrawParameterField(ObjectNames.NicifyVariableName(param.Name), param.ParameterType, mb.ParameterValues[i]);
                    }

                    if (GUILayout.Button(mb.ButtonText))
                    {
                        ExecuteMethod(mb);
                    }
                    EditorGUI.indentLevel--;
                }
            }
            else // 引数なしメソッド
            {
                if (GUILayout.Button(mb.ButtonText))
                {
                    ExecuteMethod(mb);
                }
            }
            EditorGUILayout.Space(3);
        }
    }

    private void ExecuteMethod(MethodButton methodButton)
    {
        // 静的メソッドは一回だけ実行
        if (methodButton.Method.IsStatic)
        {
            try
            {
                methodButton.Method.Invoke(null, methodButton.ParameterValues);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error executing static method '{methodButton.ButtonText}': {e.InnerException?.Message ?? e.Message}");
            }
        }
        else if (targets.Length > 1) // 複数選択時（インスタンスメソッド）
        {
            foreach (var t in targets)
            {
                Undo.RecordObject(t, $"Execute {methodButton.ButtonText} on {t.name}");
                try
                {
                    methodButton.Method.Invoke(t, methodButton.ParameterValues);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error executing '{methodButton.ButtonText}' on '{t.name}': {e.InnerException?.Message ?? e.Message}");
                }
                EditorUtility.SetDirty(t);
            }
        }
        else // 単一選択時（インスタンスメソッド）
        {
            Undo.RecordObject(target, $"Execute {methodButton.ButtonText}");
            try
            {
                methodButton.Method.Invoke(target, methodButton.ParameterValues);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error executing '{methodButton.ButtonText}': {e.InnerException?.Message ?? e.Message}");
            }
            EditorUtility.SetDirty(target);
        }

        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }

    private object DrawParameterField(string name, Type type, object value)
    {
        // 基本型
        if (type == typeof(int))
            return EditorGUILayout.IntField(name, (int)(value ?? 0));
        if (type == typeof(float))
            return EditorGUILayout.FloatField(name, (float)(value ?? 0f));
        if (type == typeof(double))
            return EditorGUILayout.DoubleField(name, (double)(value ?? 0d));
        if (type == typeof(bool))
            return EditorGUILayout.Toggle(name, (bool)(value ?? false));
        if (type == typeof(string))
            return EditorGUILayout.TextField(name, (string)value); // null許容

        // ベクトル/構造体
        if (type == typeof(Vector2))
            return EditorGUILayout.Vector2Field(name, (Vector2)(value ?? Vector2.zero));
        if (type == typeof(Vector3))
            return EditorGUILayout.Vector3Field(name, (Vector3)(value ?? Vector3.zero));
        if (type == typeof(Vector4))
            return EditorGUILayout.Vector4Field(name, (Vector4)(value ?? Vector4.zero));
        if (type == typeof(Vector2Int))
            return EditorGUILayout.Vector2IntField(name, value is Vector2Int v2i ? v2i : Vector2Int.zero);
        if (type == typeof(Vector3Int))
            return EditorGUILayout.Vector3IntField(name, value is Vector3Int v3i ? v3i : Vector3Int.zero);
        if (type == typeof(Color))
            return EditorGUILayout.ColorField(name, (Color)(value ?? Color.white));
        if (type == typeof(Rect))
            return EditorGUILayout.RectField(name, (Rect)(value ?? Rect.zero));
        if (type == typeof(Bounds))
            return EditorGUILayout.BoundsField(name, value is Bounds b ? b : new Bounds(Vector3.zero, Vector3.one));

        // その他 Unity 型
        if (type == typeof(AnimationCurve))
            return EditorGUILayout.CurveField(name, (AnimationCurve)(value ?? new AnimationCurve()));
        if (type == typeof(Gradient)) // Unity 2019.3+
            return EditorGUILayout.GradientField(name, (Gradient)(value ?? new Gradient()));
        if (type == typeof(LayerMask))
        {
            int layer = value is LayerMask lm ? lm.value : 0;
            int newLayer = EditorGUILayout.LayerField(name, layer);
            return (LayerMask)newLayer;
        }
        if (type.IsEnum)
        {
            var enumValue = (Enum)(value ?? Activator.CreateInstance(type));
            bool isFlags = Attribute.IsDefined(type, typeof(FlagsAttribute));
            return isFlags ? EditorGUILayout.EnumFlagsField(name, enumValue) : EditorGUILayout.EnumPopup(name, enumValue);
        }
        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            return EditorGUILayout.ObjectField(name, (UnityEngine.Object)value, type, true); // allowSceneObjects = true

        // 配列/リスト
        if (TryHandleListLike(name, type, value, out object listResult))
        {
            return listResult;
        }

        // System.Serializableなクラスや構造体の基本的なサポート (限定的)
        if (IsPlainSerializableClassOrStruct(type))
        {
            EditorGUILayout.LabelField(name, $"({type.Name})");
            EditorGUI.indentLevel++;
            object instance = value;
            if (instance == null)
            {
                try { instance = Activator.CreateInstance(type); }
                catch
                {
                    EditorGUILayout.HelpBox($"Cannot create instance of {type.Name}. Ensure it has a parameterless constructor.", MessageType.Warning);
                    EditorGUI.indentLevel--;
                    return value;
                }
            }

            // 公開 + [SerializeField] の非公開フィールドのみ
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                bool isSerializableField = field.IsPublic || Attribute.IsDefined(field, typeof(SerializeField));
                if (!isSerializableField) continue;

                if (IsTypeSupported(field.FieldType))
                {
                    object fieldValue = field.GetValue(instance);
                    object newFieldValue = DrawParameterField(ObjectNames.NicifyVariableName(field.Name), field.FieldType, fieldValue);
                    if (!Equals(fieldValue, newFieldValue))
                    {
                        field.SetValue(instance, newFieldValue);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(field.Name), $"(Unsupported type: {field.FieldType.Name})");
                }
            }
            EditorGUI.indentLevel--;
            return instance;
        }

        EditorGUILayout.LabelField(name, $"(Unsupported type: {type.Name})");
        return value; // サポート外の型はそのまま返す
    }

    /// <summary>
    /// プレーンなシリアライズ可能なクラスまたは構造体かどうかを判定します。
    /// (UnityEngine.Object派生、プリミティブ、Enum、Unity組み込み構造体を除く)
    /// </summary>
    private static bool IsPlainSerializableClassOrStruct(Type type)
    {
        return Attribute.IsDefined(type, typeof(SerializableAttribute)) &&
               !typeof(UnityEngine.Object).IsAssignableFrom(type) &&
               !type.IsPrimitive &&
               type != typeof(string) &&
               !type.IsEnum &&
               !IsUnityBuiltInStruct(type);
    }

    private static bool IsUnityBuiltInStruct(Type type)
    {
        return type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4) ||
               type == typeof(Vector2Int) || type == typeof(Vector3Int) ||
               type == typeof(Quaternion) || type == typeof(Matrix4x4) || type == typeof(Color) ||
               type == typeof(Rect) || type == typeof(AnimationCurve) || type == typeof(Gradient) ||
               type == typeof(Bounds);
    }
    
    /// <summary>
    /// DrawParameterField でサポートされている型かどうかを判定します。
    /// </summary>
    private static bool IsTypeSupported(Type type)
    {
        if (type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(bool) || type == typeof(string) ||
            type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4) ||
            type == typeof(Vector2Int) || type == typeof(Vector3Int) ||
            type == typeof(Color) || type == typeof(Rect) || type == typeof(Bounds) || type == typeof(AnimationCurve) ||
            type == typeof(Gradient) || type == typeof(LayerMask) || type.IsEnum || typeof(UnityEngine.Object).IsAssignableFrom(type) ||
            IsPlainSerializableClassOrStruct(type) || IsSupportedArrayOrList(type))
        {
            return true;
        }
        return false;
    }

    private static bool IsSupportedArrayOrList(Type type)
    {
        if (type.IsArray)
        {
            var elem = type.GetElementType();
            return elem != null && IsTypeSupported(elem);
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elem = type.GetGenericArguments()[0];
            return IsTypeSupported(elem);
        }
        return false;
    }

    private bool TryHandleListLike(string name, Type type, object value, out object result)
    {
        result = value;
        if (type.IsArray)
        {
            var elemType = type.GetElementType();
            if (elemType == null || !IsTypeSupported(elemType)) return false;

            Array array = value as Array ?? Array.CreateInstance(elemType, 0);
            int size = EditorGUILayout.IntField(name + " Size", array.Length);
            size = Mathf.Max(0, size);
            if (size != array.Length)
            {
                var newArray = Array.CreateInstance(elemType, size);
                int copy = Mathf.Min(size, array.Length);
                for (int i = 0; i < copy; i++) newArray.SetValue(array.GetValue(i), i);
                array = newArray;
            }
            EditorGUI.indentLevel++;
            for (int i = 0; i < array.Length; i++)
            {
                var elemValue = array.GetValue(i);
                var newElemValue = DrawParameterField($"Element {i}", elemType, elemValue);
                if (!Equals(elemValue, newElemValue)) array.SetValue(newElemValue, i);
            }
            EditorGUI.indentLevel--;
            result = array;
            return true;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elemType = type.GetGenericArguments()[0];
            if (!IsTypeSupported(elemType)) return false;

            IList list = value as IList ?? (IList)Activator.CreateInstance(type);
            int size = EditorGUILayout.IntField(name + " Size", list.Count);
            size = Mathf.Max(0, size);
            while (list.Count < size) list.Add(elemType.IsValueType ? Activator.CreateInstance(elemType) : null);
            while (list.Count > size) list.RemoveAt(list.Count - 1);

            EditorGUI.indentLevel++;
            for (int i = 0; i < list.Count; i++)
            {
                var elemValue = list[i];
                var newElemValue = DrawParameterField($"Element {i}", elemType, elemValue);
                if (!Equals(elemValue, newElemValue)) list[i] = newElemValue;
            }
            EditorGUI.indentLevel--;
            result = list;
            return true;
        }
        return false;
    }
}