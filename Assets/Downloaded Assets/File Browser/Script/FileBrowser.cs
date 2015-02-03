﻿#region

using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

#endregion

public class FileBrowser
{
	public GUIStyle backStyle;
	public GUIStyle cancelStyle;
	public GUIStyle confirmStyle;
	private DirectoryInfo currentDirectory;
	private Color defaultColor;
	private GUISkin defaultSkin;
	private DirectoryInformation[] directories, drives;
	private Vector2 directoryScroll = Vector2.zero;
	private Vector2 driveScroll = Vector2.zero;
	private FileInformation[] files;
	private Vector2 fileScroll = Vector2.zero;
	public Texture fileTexture, directoryTexture, backTexture, driveTexture;
	public GUISkin guiSkin;
	private bool isSearching;
	private bool justFinishedSearching;
	public FileInfo outputFile;
	private DirectoryInformation parentDirectory;
	public bool recursiveSearch = true;
	private float searchStartTime;
	private string searchString = "";
	public Color selectedColor = new Color(0.5f, 0.5f, 0.9f);
	private int selectedIndex = -1;
	private bool showDrives;
	public bool showSearchBar = true;

	public FileBrowser(string directory) { GetFileList(currentDirectory = new DirectoryInfo(directory)); }

	public FileBrowser() : this(Directory.GetCurrentDirectory()) { }

	public bool Draw()
	{
		var value = false;
		if (justFinishedSearching && Event.current.type == EventType.Layout)
		{
			justFinishedSearching = isSearching = false;
			SelectFile(selectedIndex);
		}
		if (GUI.GetNameOfFocusedControl() == "")
			GUI.FocusControl("SearchBar");
		if (guiSkin)
		{
			defaultSkin = GUI.skin;
			GUI.skin = guiSkin;
		}
		GUILayout.BeginArea(new Rect(Screen.width * 0.15f, Screen.height * 0.1f, Screen.width * 0.7f, Screen.height * 0.8f));
		GUILayout.BeginVertical("box");
		GUILayout.BeginHorizontal("box");
		GUILayout.FlexibleSpace();
		GUILayout.Label(currentDirectory.FullName);
		GUILayout.FlexibleSpace();
		if (showSearchBar)
			DrawSearchBar();
		GUILayout.EndHorizontal();
		GUILayout.BeginHorizontal("box");
		GUILayout.BeginVertical(GUILayout.Width(Screen.width * 0.25f));
		if (showDrives)
		{
			GUILayout.BeginVertical("box");
			driveScroll = GUILayout.BeginScrollView(driveScroll);
			foreach (var drive in drives.Where(drive => drive.Button()))
				GetFileList(drive.directoryInfo);
			GUILayout.EndScrollView();
			GUILayout.EndVertical();
		}
		else if (parentDirectory.Button(backStyle ?? new GUIStyle("button") { alignment = TextAnchor.MiddleCenter }))
			GetFileList(parentDirectory.directoryInfo);
		GUILayout.BeginVertical("box");
		directoryScroll = GUILayout.BeginScrollView(directoryScroll);
		foreach (var directory in directories.Where(directory => directory.Button()))
			GetFileList(directory.directoryInfo);
		GUILayout.EndScrollView();
		GUILayout.EndVertical();
		GUILayout.EndVertical();
		GUILayout.BeginVertical();
		GUILayout.BeginVertical("box");
		if (isSearching)
			DrawSearchMessage();
		else
		{
			fileScroll = GUILayout.BeginScrollView(fileScroll);
			for (var i = 0; i < files.Length; i++)
			{
				if (i == selectedIndex)
				{
					defaultColor = GUI.color;
					GUI.color = selectedColor;
				}
				GUI.SetNextControlName(i.ToString());
				if (files[i].Button())
					SelectFile(i);
				if (i == selectedIndex)
					GUI.color = defaultColor;
			}
			GUILayout.EndScrollView();
		}
		GUILayout.EndVertical();
		GUILayout.BeginHorizontal("box");
		GUILayout.FlexibleSpace();
		if (GUILayout.Button("确定", confirmStyle ?? "button") || GUI.GetNameOfFocusedControl() != "SearchBar" && Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.Return)
			value = true;
		GUILayout.FlexibleSpace();
		if (GUILayout.Button("取消", cancelStyle ?? "button") || Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.Escape)
		{
			SelectFile(-1);
			value = true;
		}
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
		GUILayout.EndVertical();
		GUILayout.EndHorizontal();
		GUILayout.EndVertical();
		GUILayout.EndArea();
		if (guiSkin)
			GUI.skin = defaultSkin;
		if (Event.current.type != EventType.KeyDown)
			return value;
		if (Event.current.keyCode == KeyCode.UpArrow)
			SelectLast();
		if (Event.current.keyCode == KeyCode.DownArrow)
			SelectNext();
		return value;
	}

	private void DrawSearchBar()
	{
		if (isSearching)
			GUILayout.Label("正在搜索：\"" + searchString + "\"");
		else
		{
			GUI.SetNextControlName("SearchBar");
			searchString = GUILayout.TextField(searchString, GUILayout.MinWidth(Screen.width * 0.2f));
			if (GUILayout.Button("搜索") || GUI.GetNameOfFocusedControl() == "SearchBar" && Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.Return)
			{
				isSearching = true;
				searchStartTime = Time.time;
				new Thread(SearchFile).Start();
			}
		}
		GUILayout.Space(Screen.width * 0.01f);
	}

	private void DrawSearchMessage()
	{
		var elapsedTime = Time.time - searchStartTime;
		if (elapsedTime > 1)
			GUILayout.Button("正在");
		if (elapsedTime > 2)
			GUILayout.Button("搜索");
		if (elapsedTime > 3)
			GUILayout.Button("\"" + searchString + "\"");
		if (elapsedTime > 4)
			GUILayout.Button("……");
		if (elapsedTime > 5)
			GUILayout.Button("这将");
		if (elapsedTime > 6)
			GUILayout.Button("花费");
		if (elapsedTime > 7)
			GUILayout.Button("一点");
		if (elapsedTime > 8)
			GUILayout.Button("时间");
		if (elapsedTime > 9)
			GUILayout.Button("……");
	}

	private void GetFileList(DirectoryInfo directory)
	{
		SelectFile(-1);
		currentDirectory = directory;
		parentDirectory = new DirectoryInformation(directory.Parent ?? directory, backTexture);
		showDrives = directory.Parent == null;
		var driveNames = Directory.GetLogicalDrives();
		drives = new DirectoryInformation[driveNames.Length];
		for (var i = 0; i < drives.Length; i++)
			drives[i] = new DirectoryInformation(new DirectoryInfo(driveNames[i]), driveTexture);
		var directoryInfos = directory.GetDirectories();
		directories = new DirectoryInformation[directoryInfos.Length];
		for (var i = 0; i < directories.Length; i++)
			directories[i] = new DirectoryInformation(directoryInfos[i], directoryTexture);
		var fileInfos = directory.GetFiles();
		files = new FileInformation[fileInfos.Length];
		for (var i = 0; i < files.Length; i++)
			files[i] = new FileInformation(fileInfos[i], fileTexture);
	}

	public void Refresh() { GetFileList(currentDirectory); }

	private void SearchFile()
	{
		var fileInfos = searchString == "" ? currentDirectory.GetFiles() : currentDirectory.GetFiles(searchString, recursiveSearch ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
		files = new FileInformation[fileInfos.Length];
		if (fileInfos.Length == 0)
			selectedIndex = -1;
		else
		{
			for (var i = 0; i < files.Length; i++)
				files[i] = new FileInformation(fileInfos[i], fileTexture);
			selectedIndex = 0;
		}
		justFinishedSearching = true;
	}

	private void SelectFile(int index)
	{
		outputFile = (selectedIndex = index) < 0 ? null : files[index].fileInfo;
		GUI.FocusControl(index.ToString());
	}

	public void SelectLast() { SelectFile(selectedIndex = (selectedIndex + files.Length - 1) % files.Length); }

	public void SelectNext() { SelectFile(selectedIndex = (selectedIndex + 1) % files.Length); }
}