using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection.Emit;

public partial class SettingsMenu : Control
{
	[Signal]
	public delegate void DialogResultEventHandler();

	Button closeButton, toggleButton, resetColorButton, importTemplate, resetTemplate;
	ColorPickerButton colorPicker;
	Godot.Label drawModeLabel;
	FileDialog fileDialog;	

	TextureRect cardTemplate;
	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		closeButton = GetNode<Button>("./Panel/CloseButton");
		toggleButton = GetNode<Button>("./Panel/ToggleDrawModeButton");
		resetColorButton = GetNode<Button>("./Panel/ResetColorButton");
		colorPicker = GetNode<ColorPickerButton>("./Panel/BoardColor");
		drawModeLabel = GetNode<Godot.Label>("./Panel/DrawMode");
		cardTemplate = GetNode<TextureRect>("./Panel/CardTemplate");
		importTemplate = GetNode<Button>("./Panel/ImportTemplateButton");
		resetTemplate = GetNode<Button>("./Panel/ResetTemplateButton");

		toggleButton.Pressed += ToggleDrawMode;
		resetColorButton.Pressed += ResetColor;
		importTemplate.Pressed += FileImport;
		resetTemplate.Pressed += ResetTemplate;
		closeButton.Pressed += CloseSettings;

		if (Settings.drawMode == 3)
		{
			drawModeLabel.Text = "3 Cards";
		}
		else
		{
			drawModeLabel.Text = "1 Card";
		}
		colorPicker.Color = new Godot.Color(Settings.hexColor);
		cardTemplate.Texture = Settings.cardTemplate;
	}

	private void ToggleDrawMode()
	{
		if (drawModeLabel.Text == "3 Cards")
		{
			drawModeLabel.Text = "1 Card";
		}
		else
		{
			drawModeLabel.Text = "3 Cards";
		}
	}

	private void ResetColor()
	{
		colorPicker.Color = new Godot.Color("378839");
	}

	private void FileImport()
	{
   	 	string[] filters = { "*.png, *.jpg, *.jpeg ; Supported Images" };

		Callable call = Callable.From<bool, string[], int>(OnFileSelected);

    	// Show the dialog
    	DisplayServer.FileDialogShow
		(
        	"Open Image",           // Title
        	"",                     // Initial directory (empty for default)
        	"",                     // Default filename
        	false,                  // Show hidden files
        	DisplayServer.FileDialogMode.OpenFile, 
        	filters,
        	call // Callback method
    	);
	}

	private void OnFileSelected(bool status, string[] selectedPaths, int selectedFilterIndex)
	{
	    if (!status || selectedPaths.Length == 0) return;

	    string filePath = selectedPaths[0];

	    byte[] buffer = Godot.FileAccess.GetFileAsBytes(filePath);

	    if (buffer == null || buffer.Length == 0)
	    {
	        return;
	    }

	    Image img = new Image();
	    Error err = Error.Failed;

	    // Try each format manually since the extension is missing
	    if (img.LoadPngFromBuffer(buffer) == Error.Ok) { err = Error.Ok; }
	    else if (img.LoadJpgFromBuffer(buffer) == Error.Ok) { err = Error.Ok; }
	    else if (img.LoadWebpFromBuffer(buffer) == Error.Ok) { err = Error.Ok; }
	    else if (img.LoadTgaFromBuffer(buffer) == Error.Ok) { err = Error.Ok; }

	    if (err == Error.Ok)
	    {
	        img.Resize(71, 96);
	        ImageTexture texture = ImageTexture.CreateFromImage(img);
	        cardTemplate.Texture = texture;
	    }
	}
	private void ResetTemplate()
	{
		Settings.cardTemplate = (Texture2D) GD.Load("res://fotos/Tardis.png");
		cardTemplate.Texture = Settings.cardTemplate;
	}

	private void FileSelected(string path)
	{
		string fileName;
		int index, index2;

		index =  path.LastIndexOf('/');
		index ++;
		fileName = path[index..];

		Image img = Image.LoadFromFile(path);

		if (!File.Exists("user://Card_Templates/" + fileName))
		{
			index2 =  path.LastIndexOf('.');
			fileName = path.Substr(index, index2 - index);
			img.Resize(71, 96);
			img.SaveJpg("user://Card_Templates/" + fileName + ".jpg");
		}

		img.Resize(71,96);
		var tex = new ImageTexture();
		tex.SetImage(img);
		
		cardTemplate.Texture = (Texture2D) tex;

		fileDialog.QueueFree();
	}

	private void FileCancel()
	{
		fileDialog.QueueFree();
	}

	private void CloseSettings()
	{
		if (drawModeLabel.Text == "3 Cards")
		{
			Settings.drawMode = 3;
		}
		else
		{
			Settings.drawMode = 1;
		}

		Settings.hexColor = colorPicker.Color.ToHtml();
		Settings.cardTemplate = cardTemplate.Texture;

		EmitSignal(SignalName.DialogResult);
		QueueFree();
	}
}
