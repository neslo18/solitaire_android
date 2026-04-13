using Godot;
using System;

public partial class NewGameMessage : Control
{
	[Signal]
	public delegate void DialogResultEventHandler(bool accepted);

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		
		Button yes = GetNode<Button>("./Panel/yesButton");
		Button no = GetNode<Button>("./Panel/noButton");

		yes.Pressed += OnYes;
		no.Pressed += OnNo;
	}

	private void OnNo()
	{
		EmitSignal(SignalName.DialogResult, false);
		QueueFree();
	}

	private void OnYes()
	{
		EmitSignal(SignalName.DialogResult, true);
		QueueFree();
	}
}
