using System;
using UnityEngine;


#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine.Assertions.Must;
[CanEditMultipleObjects]
[CustomEditor(typeof(UnityEngine.Object), true, isFallback = false)]
public class InspectorAdvance : Editor
{

	MethodInfo currentMethod;
	object[] arguments;
	ParameterInfo currentParameter;
	int intTypeField;
	float floatTypeField;
	bool boolTypeField;
	string stringTypeField;
	bool isFoldOut;

	FetchedData[] functionlist;

	void OnEnable()
	{
		List<FetchedData> dataList = new List<FetchedData>();
		Type type = target.GetType();
		FetchedData data;
		MethodInfo[] methodInfo = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		for (int i = 0; i < methodInfo.Length; i++)
		{
			ButtonInspector attribute = Attribute.GetCustomAttribute(methodInfo[i], typeof(ButtonInspector)) as ButtonInspector;
			if (attribute != null)
			{
				data = new FetchedData();
				data.buttonName = attribute.buttonName.Equals("") ? methodInfo[i].Name : attribute.buttonName;
				data.args = attribute.args;
				data.mode = attribute.mode;
				data.method = methodInfo[i];

				dataList.Add(data);
			}
		}
		if (dataList.Count > 0)
		{
			functionlist = dataList.ToArray();
		}
		else {
			functionlist = null;
		}
	}


	public override void OnInspectorGUI()
	{
		if (functionlist != null)
		{
			EditorGUILayout.Space();

			for (int i = 0; i < functionlist.Length; i++)
			{
				DisplayFunction(functionlist[i]);
			}
			EditorGUILayout.Space();
		}
		DrawDefaultInspector();
	}

	void DisplayFunction(FetchedData data)
	{
		if (data.mode == ButtonWorkMode.Both || data.mode == ButtonWorkMode.BothAndAsk)
		{
			if (data.mode == ButtonWorkMode.BothAndAsk)
			{
				DrawButtonAskAndInvoke(data);
			}
			else
			{
				DrawButtonAndInvoke(data);
			}
		}
		else
		{
			if (Application.isPlaying)
			{
				if (data.mode == ButtonWorkMode.RuntimeOnly)
				{
					DrawButtonAndInvoke(data);
				}
				if (data.mode == ButtonWorkMode.RuntimeOnlyAndAsk)
				{
					DrawButtonAskAndInvoke(data);
				}
			}
			else
			{
				if (data.mode == ButtonWorkMode.EditorOnly)
				{
					DrawButtonAndInvoke(data);
				}
				if (data.mode == ButtonWorkMode.EditorOnlyAndAsk)
				{
					DrawButtonAskAndInvoke(data);
				}
			}
		}
	}

	void DrawButtonAndInvoke(FetchedData data)
	{
		if (GUILayout.Button(data.buttonName))
		{
			foreach (var item in targets)
			{
				data.method.Invoke(item, data.args);
			}
		}
	}

	void DrawButtonAskAndInvoke(FetchedData attribute)
	{
		if (GUILayout.Button(attribute.buttonName))
		{
			bool askedResult = EditorUtility.DisplayDialog("Invoke Function", "Do you wants to invoke " + attribute.buttonName, "Yes", "No");
			if (askedResult)
			{
				foreach (var item in targets)
				{
					attribute.method.Invoke(item, attribute.args);
				}
			}
		}
	}

	class FetchedData
	{
		public string buttonName;
		public ButtonWorkMode mode;
		public object[] args;
		public MethodInfo method;
	}

}

#endif

[AttributeUsage(AttributeTargets.Method)]
public class ButtonInspector : Attribute
{
	public string buttonName;
	public ButtonWorkMode mode;
	public object[] args;

	/// <summary>
	/// Initializes a new instance of the <see cref="T:ButtonInspector"/> class.
	/// </summary>
	public ButtonInspector() : this(string.Empty, ButtonWorkMode.Both, new object[0])
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="T:ButtonInspector"/> class.
	/// </summary>
	/// <param name="buttonName">Button name.</param>
	/// <param name="mode">Mode.</param>
	/// <param name="args">Arguments.</param>
	public ButtonInspector(string buttonName, ButtonWorkMode mode, params object[] args)
	{
		this.buttonName = buttonName;
		this.mode = mode;
		this.args = args;
	}
	/// <summary>
	/// Initializes a new instance of the <see cref="T:ButtonInspector"/> class.
	/// </summary>
	/// <param name="buttonName">Button name.</param>
	public ButtonInspector(string buttonName) : this(buttonName, ButtonWorkMode.Both, new object[0])
	{
	}
	/// <summary>
	/// Initializes a new instance of the <see cref="T:ButtonInspector"/> class.
	/// </summary>
	/// <param name="mode">Mode.</param>
	public ButtonInspector(ButtonWorkMode mode) : this(string.Empty, mode, new object[0])
	{
	}
	/// <summary>
	/// Initializes a new instance of the <see cref="T:ButtonInspector"/> class.
	/// </summary>
	/// <param name="args">Arguments.</param>
	public ButtonInspector(params object[] args) : this(string.Empty, ButtonWorkMode.Both, args)
	{
	}
	/// <summary>
	/// Initializes a new instance of the <see cref="T:ButtonInspector"/> class.
	/// </summary>
	/// <param name="buttonName">Button name.</param>
	/// <param name="mode">Mode.</param>
	public ButtonInspector(string buttonName, ButtonWorkMode mode) : this(buttonName, mode, new object[0])
	{
	}
	/// <summary>
	/// Initializes a new instance of the <see cref="T:ButtonInspector"/> class.
	/// </summary>
	/// <param name="mode">Mode.</param>
	/// <param name="args">Arguments.</param>
	public ButtonInspector(ButtonWorkMode mode, params object[] args) : this(string.Empty, mode, args)
	{
	}
	/// <summary>
	/// Initializes a new instance of the <see cref="T:ButtonInspector"/> class.
	/// </summary>
	/// <param name="buttonName">Button name.</param>
	/// <param name="args">Arguments.</param>
	public ButtonInspector(string buttonName, params object[] args) : this(buttonName, ButtonWorkMode.Both, args)
	{
	}
}

[Flags]
public enum ButtonWorkMode
{
	EditorOnly = 1,
	RuntimeOnly = 2,
	EditorOnlyAndAsk = 3,
	RuntimeOnlyAndAsk = 4,
	Both = 5,
	BothAndAsk = 6
}
