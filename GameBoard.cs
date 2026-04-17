using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class GameBoard : Node2D
{
	private bool gameLoading = true;
	private PackedScene _newGameDialog = GD.Load<PackedScene>("res://newGameMessage.tscn");
	private PackedScene _settingsDialog = GD.Load<PackedScene>("res://settingsMenu.tscn");
	private readonly static Texture emptyDeckImg = (Texture) GD.Load("res://fotos/Empty_Deck.png");
	private int offset = 25, drawMode = 3, totalTime = 0;
	private double timeElapsed = 0f;
	private ColorRect backColor;
	private readonly List<int> scoreMoves = [];
	private List<CardObject> everyCard;
	private readonly List<List<String>> Moves = [];
	private readonly List<Table> TableMove = [];
	private readonly CustomList deck = new([], "deck"), drawnDeck = new([], "drawnDeck");
	private readonly CustomList[] tabluea = new CustomList[7];
	private readonly CustomList[] foundations = new CustomList[4];
	private readonly TextureRect[] tableauSlots = new TextureRect[7];
	private readonly TextureRect[] foundationSlots = new TextureRect[4];
	private TextureButton deckSlot;
	private bool pauseTree = true;

	// occurs when screen redraws
	public override void _Process(double delta)
	{
		if (gameLoading)
		{
			return;
		}
		timeElapsed += delta;

		// updates time on clock
		if (timeElapsed >= 1.0)
		{
			timeElapsed = 0f;
			if (totalTime < 5999)
			{
				totalTime += 1;
			}

			// does not update clock if won
			if (!CheckWon())
			{
				Label timeLabel = GetNode<Label>("./timeContainer/timeValue");
				TimeSpan t = TimeSpan.FromSeconds(totalTime);
				string formatted = t.ToString(@"mm\:ss");
				timeLabel.Text = formatted;
			}
		}
	}

	// occurs when node enters the scene
	public override async void _Ready()
	{
		OS.RequestPermissions();
		deckSlot = GetNode<TextureButton>("./deckSlot");

		// checks for saved settings and loads them into game
		if (Godot.FileAccess.FileExists("user://solitaireSettings.save"))
		{
			using var saveFile = Godot.FileAccess.Open("user://solitaireSettings.save", Godot.FileAccess.ModeFlags.Read);

			var line = saveFile.GetLine();
			if (line == "3")
			{
				Settings.drawMode = 3;
			}
			else
			{
				Settings.drawMode = 1;
			}

			Settings.hexColor = saveFile.GetLine();
		}
		else
		{
			Settings.drawMode = 3;
			Settings.hexColor = "378839";
		}

		// checks for saved template of card deck
		// loads it if found
		if (Godot.FileAccess.FileExists("user://solitaireTemplate.save"))
		{
			using var saveFile = Godot.FileAccess.Open("user://solitaireTemplate.save", Godot.FileAccess.ModeFlags.Read);

			var data = (Godot.Collections.Dictionary)saveFile.GetVar();
			byte[] imgData = (byte[])data["texture_buffer"];
			Image img = new();
			img.LoadPngFromBuffer(imgData);
			Settings.cardTemplate = ImageTexture.CreateFromImage(img);
		}
		else
		{
			GD.Print("def");
			Settings.cardTemplate = ImageTexture.CreateFromImage(Settings.AddBackground(GD.Load<Texture2D>("res://fotos/default_face.png").GetImage()));
		}

		// applies new or old settings
		drawMode = Settings.drawMode;
		backColor = GetNode<ColorRect>("./backColor");
		backColor.Color = new Color(Settings.hexColor);
		deckSlot.TextureNormal = Settings.cardTemplate;

		// loads card template to be loaded into 52 cards
		var cardObj = GD.Load<PackedScene>("res://CardObject.tscn");
		Random random = new();
		List<CardObject> AllCards = [];

		// assigns signals to all bottom menu buttons
		TextureButton newGame = GetNode<TextureButton>("./subMenu/TextureRect/HBoxContainer/newGameButton");
		newGame.Pressed += NewGame;

		TextureButton hintButton = GetNode<TextureButton>("./subMenu/TextureRect/HBoxContainer/hintButton");
		hintButton.Pressed += Hint;

		TextureButton undoButton = GetNode<TextureButton>("./subMenu/TextureRect/HBoxContainer/undoButton");
		undoButton.Pressed += Undo;

		TextureButton settingsButton = GetNode<TextureButton>("./subMenu/TextureRect/HBoxContainer/settingsButton");
		settingsButton.Pressed += OpenSettings;

		// assigns all tableau slots to array for easy matching locations
		HBoxContainer tableauContainer = GetNode<HBoxContainer>("./tableauContainer");
		for (int i = 0; i < tableauContainer.GetChildCount(); i ++)
		{
			tableauSlots[i] = (TextureRect) tableauContainer.GetChild(i);
		}

		// assigns all foundation slots to array for easy matching locations
		HBoxContainer foundationContainer = GetNode<HBoxContainer>("./foundationContainer");
		for (int i = 0; i < foundationContainer.GetChildCount(); i ++)
		{
			foundationSlots[i] = (TextureRect) foundationContainer.GetChild(i);
		}

		// creates all 52 cards
		for (int i = 1; i < 5; i++)
		{
			for (int j = 1; j < 14; j++)
			{
				var newCard = cardObj.Instantiate<CardObject>();
				newCard.Position = deckSlot.Position;
				AllCards.Add(newCard.Init(i, j));
				newCard.ScoreChange += UpdateScore; 
				newCard.CardMoved += CardMoved;
				newCard.CardDoubleClicked += AutoMove;
				AddChild(newCard);
				newCard.FlipCard();
			}
		}

		// copies data of all cards for easier queries for updating
		everyCard = [..AllCards];

		// waits one frame to get correct positioning
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		// assigns 28 cards to the tableau randomly then flips alls the cards except bottom of each stack
		for (int i = 0; i < tabluea.Length; i++)
		{
			tabluea[i] = new([], "table" + i);

			for (int j = 0; j < (i + 1); j++)
			{
				int randomNum = random.Next(0, AllCards.Count);
				AllCards[randomNum].MoveToFront();
				//AllCards[randomNum].GlobalPosition = new Vector2(tableauSlots[i].GlobalPosition.X, tableauSlots[i].GlobalPosition.Y + (j * offset));
				AllCards[randomNum].SnapNewPos(new Vector2(tableauSlots[i].GlobalPosition.X, tableauSlots[i].GlobalPosition.Y + (j * offset)));
				await Task.Delay(100);
				AllCards[randomNum].ChangeList(tabluea[i]);
				AllCards.RemoveAt(randomNum);
			}
			tabluea[i].list[^1].FlipCard(true);
			tabluea[i].list[^1].SetMovable(true);
		}

		// assigns the remaining 24 cards to the deck
		for (int i = 0; i < 24; i ++)
		{
			int randomNum = random.Next(0, AllCards.Count);
			AllCards[randomNum].GlobalPosition = deckSlot.GlobalPosition;
			AllCards[randomNum].ChangeList(deck);
			AllCards.RemoveAt(randomNum);
		}

		// deck signal
		deckSlot.Pressed += DeckDraw;
		deckSlot.MoveToFront();

		foundations[0] = new([], "foundation0");
		foundations[1] = new([], "foundation1");
		foundations[2] = new([], "foundation2");
		foundations[3] = new([], "foundation3");

		List<String> newMove = [];

		//saves all startup data of everycard for undo function to operate
		foreach (CardObject card in everyCard)
		{
			newMove.Add(card.SaveCardData());
		}

		Moves.Add(newMove);
		TableMove.Add(new Table(deck, drawnDeck, tabluea, foundations, deckSlot.TextureNormal));
		scoreMoves.Add(0);

		foreach (CardObject card in everyCard)
		{
			card.setGameLoaded();
		}
		gameLoading = false;
	}

	// determines if won
	private bool CheckWon()
	{
		if (foundations[0] == null)
		{
			return false;
		}
		return foundations[0].list.Count == 13 && foundations[1].list.Count == 13 && foundations[2].list.Count == 13 && foundations[3].list.Count == 13;
	}

	// shows fireworks if won
	private async void ShowFireworks()
	{
		Control fireworks = GetNode<Control>("./fireworks");

		fireworks.Visible = true;
		
		int finishBonus = 700000 / totalTime;
		UpdateScore(finishBonus);

		for (int i = 0; i < 4; i++)
		{
			foreach (CardObject card in foundations[i].list)
			{
				card.SetMovable(false);
			}
		}
		await Task.Delay(5000);

		pauseTree = false;
		NewGame();
	}

	// attempts to draw new card from deck, or flip deck over
	private void DeckDraw()
	{

		if (gameLoading)
		{
			return;
		}
		// reset drawn into deck if reached 0
		// or align cards in order being newly drawn cards
		foreach (CardObject card in drawnDeck.list)
		{
			if (deck.list.Count == 0)
			{
				card.Position = new Vector2(deckSlot.Position.X, card.Position.Y);
				card.FlipCard();
			}
			else
			{
				card.Position = new Vector2(414, card.Position.Y);	
			}
			card.SetMovable(false);
		}
		
		// if deck is 0 and drawn cards is not 0 then flip them over into deck to restart sequence
		// skips rest of method
		if (deck.list.Count == 0 && drawnDeck.list.Count > 0)
		{
			deckSlot.MoveToFront();
			int totalCount = drawnDeck.list.Count;

			for (int i = 0; i < totalCount; i++)
			{
				drawnDeck.list[0].ChangeList(deck, false);
			}
			deckSlot.TextureNormal = (Texture2D)Settings.cardTemplate;
			return;
		}

		// attempts to draw as manys codes are draw mode allowed
		// flips card, brings them to front and aligns them to the left of deck
		for (int i = 0; i < drawMode; i++)
		{
			if (deck.list.Count > 0)
			{
				deck.list[0].MoveToFront();
				deck.list[0].FlipCard();
				deck.list[0].Position = new Vector2(414 + (i * offset), deck.list[0].Position.Y);
				deck.list[0].ChangeList(drawnDeck, false, true);
			}
		}

		// changes deck texture if empty
		if (deck.list.Count == 0 && drawnDeck.list.Count > 0)
		{
			deckSlot.TextureNormal = (Texture2D)emptyDeckImg;
		}	
		
		// allows highest card in drawn deck to move
		drawnDeck.list[^1].SetMovable(true);
		UpdateMoves();
	}

	// signal triggered when card moved.
	private void CardMoved(CardObject movedCard, Vector2 orgPos)
	{
		if (!SnapToTableau(movedCard) && !SnapToFoundation(movedCard))
		{
			movedCard.ReturnToOrigin();
		}
		else
		{
			UpdateMoves();
		}
	}

	// auto move logic. Double click card moves to first available slot if found
	private async void AutoMove(CardObject movedCard)
	{
		// tests if foundation is applicable
		CustomList FoundationSlot = TestFoundation(movedCard);
		bool skipScore = false;

		// if foundation is found then determine if score should be added and attempt card move
		if (FoundationSlot != null)
		{
			for (int i = 0; i < foundations.Length; i++)
			{
				if (foundations[i].list == FoundationSlot.list)
				{
					for (int ii = 0; ii < 4; ii++)
					{
						if (foundations[ii].list.Contains(movedCard))
						{
							skipScore = true;
						}
					}
					if (!skipScore)
					{
						UpdateScore(10);
					}
					movedCard.SnapNewPos(foundationSlots[i].GlobalPosition);
					await Task.Delay(300);
					movedCard.GetTopCard()?.SetBottomCard(null);
					movedCard.SetTopCard(null);
					movedCard.ChangeList(FoundationSlot);
					UpdateMoves();
					if (CheckWon())
					{
						ShowFireworks();
					}
					return;
				}
			}
		}
		else
		{
			// if foundation is not found then test tableau for a correct snap
			CustomList TableauSlot = TestTableau(movedCard);

			// if tableau is found then determine if score should be added, subtracted or skipped also attempt card move
			if (TableauSlot != null)
			{
				// attempt to move card K to empty slot
				if (TableauSlot.list.Count == 0)
				{
					for (int i = 0; i < tabluea.Length; i++)
					{
						if (TableauSlot == tabluea[i])
						{
							for (int ii = 0; ii < 4; ii++)
							{
								if (foundations[ii].list.Contains(movedCard))
								{
									skipScore = true;
									UpdateScore(-10);
								}
							}
							if (!drawnDeck.list.Contains(movedCard))
							{
								skipScore = true;
							}

							if (!skipScore)
							{
								UpdateScore(5);
							}
							movedCard.SnapNewPos(tableauSlots[i].GlobalPosition);
							await Task.Delay(300);
							movedCard.GetTopCard()?.SetBottomCard(null);
							movedCard.SetTopCard(null);
							movedCard.ChangeList(TableauSlot);
							UpdateMoves();
							return;
						}
					}
				}
				else
				{
					// attempt a regular card offset snap
					for (int ii = 0; ii < 4; ii++)
					{
						if (foundations[ii].list.Contains(movedCard))
						{
							skipScore = true;
							UpdateScore(-10);
							break;
						}
					}
					if (!drawnDeck.list.Contains(movedCard))
					{
						skipScore = true;
					}
					if (!skipScore)
					{
						UpdateScore(5);
					}
					Vector2 newPos = new(TableauSlot.list[^1].Position.X, TableauSlot.list[^1].Position.Y + 25);
					movedCard.SnapNewPos(newPos);
					await Task.Delay(300);
					TableauSlot.list[^1].SetBottomCard(movedCard);
					movedCard.GetTopCard()?.SetBottomCard(null);
					movedCard.SetTopCard(TableauSlot.list[^1]);
					movedCard.ChangeList(TableauSlot);
					UpdateMoves();
					return;
				}
			}
		}
	}

	// used in Auto Move to test if tableau is an applicable move
	private CustomList TestTableau(CardObject movedCard)
	{
		foreach(CustomList tableauSlot in tabluea)
		{
			if (tableauSlot.list.Contains(movedCard))
			{
				continue;
			}

			if (tableauSlot.list.Count > 0 && !tableauSlot.list[^1].SnapLocked() && movedCard.IsOppositeSuit(tableauSlot.list[^1]) && (tableauSlot.list[^1].IsPrevFace(movedCard) || movedCard.IsNextFace(tableauSlot.list[^1])))
			{
				return tableauSlot;
			}
		}

		if (movedCard.GetFace() == 13)
		{
			for (int i = 0; i < tableauSlots.Length; i++)
			{

				if (tabluea[i].list.Count == 0)
				{
					return tabluea[i];
				}
			}
		}
		return null;
	}

	// attempts to snap card to tableau by manual move
	private bool SnapToTableau(CardObject movedCard)
	{
		CustomList dest = null;
		bool snapped = false;
		bool skipScore = false;

		// checks tableau for any correct snaps
		foreach(CustomList tableauSlot in tabluea)
		{
			// skips tableau check if slot is empty
			if (tableauSlot.list.Count == 0)
			{
				continue;
			}

			Rect2 movingRect = new(movedCard.Position, (int)(71 * 1.25), (int)(96* 1.25));
			Rect2 staticRect = new(tableauSlot.list[^1].Position, (int)(71 * 1.25), (int)(96* 1.25));

			// skips tableau check if this is where card came from
			if (tableauSlot.list.Contains(movedCard))
			{
				continue;
			}

			// card must be a different color and the next number in the sequence
			if (!tableauSlot.list[^1].SnapLocked() && movingRect.Intersects(staticRect) && movedCard.IsOppositeSuit(tableauSlot.list[^1]) && (tableauSlot.list[^1].IsPrevFace(movedCard) || movedCard.IsNextFace(tableauSlot.list[^1])))
			{
				Vector2 newPos = new(tableauSlot.list[^1].Position.X, tableauSlot.list[^1].Position.Y + 25);
				movedCard.SnapNewPos(newPos);
				tableauSlot.list[^1].SetBottomCard(movedCard);
				movedCard.GetTopCard()?.SetBottomCard(null);
				movedCard.SetTopCard(tableauSlot.list[^1]);
				dest = tableauSlot;
				snapped = true;
			}
		}

		// if regular snap failed and card is a K then check if a K can be slotted to an empty location
		if (!snapped && movedCard.GetFace() == 13)
		{
			for (int i = 0; i < tableauSlots.Length; i++)
			{
				Rect2 movingRect = new(movedCard.Position, (int)(71 * 1.25), (int)(96* 1.25));
				Rect2 staticRect = new(tableauSlots[i].GlobalPosition, (int)(71 * 1.25), (int)(96* 1.25));

				if (tabluea[i].list.Count == 0 && movingRect.Intersects(staticRect))
				{
					movedCard.SnapNewPos(tableauSlots[i].GlobalPosition);
					movedCard.GetTopCard()?.SetBottomCard(null);
					movedCard.SetTopCard(null);
					dest = tabluea[i];
					snapped = true;
				}
			}
		}

		// if snap was success check if score should be added, subtracted or skipped. Also updated Card List
		if (snapped)
		{
			for (int ii = 0; ii < 4; ii++)
			{
				if (foundations[ii].list.Contains(movedCard))
				{
					skipScore = true;
					UpdateScore(-10);
					break;
				}
			}
			if (!drawnDeck.list.Contains(movedCard))
			{
				skipScore = true;
			}

			if (!skipScore)
			{
				UpdateScore(5);
			}
			movedCard.ChangeList(dest);
		}

		return snapped;
	}

	// used in Auto Move to test if foundation is an applicable move
	private CustomList TestFoundation(CardObject movedCard)
	{
		if (movedCard.GetBottomCard() != null)
		{
			return null;
		}

		for (int i = 0; i < foundationSlots.Length; i++)
		{
			if (foundations[i].list.Count == 0 || !foundations[i].list[^1].SnapLocked())
			{
				if ((foundations[i].list.Count == 0 && movedCard.GetFace() == 1) || (foundations[i].list.Count != 0 && foundations[i].list[^1].IsSameSuit(movedCard) && foundations[i].list[^1].IsNextFace(movedCard)))
				{
					return foundations[i];
				}
			}
		}

		return null;
	}

	// attempts to snap card to foundation by manual move
	private bool SnapToFoundation(CardObject movedCard)
	{
		CustomList dest = null;
		bool snapped = false;
		bool skipScore = false;

		// prevents snap if card is carrying others
		if (movedCard.GetBottomCard() != null)
		{
			return false;
		}

		// checks foundations for any correct snaps
		for (int i = 0; i < foundationSlots.Length; i++)
		{
			Rect2 movingRect = new(movedCard.Position, (int)(71 * 1.25), (int)(96 * 1.25));
			Rect2 staticRect = new(foundationSlots[i].GlobalPosition, (int)(71 * 1.25), (int)(96 * 1.25));

			if (movingRect.Intersects(staticRect) && (foundations[i].list.Count == 0 || !foundations[i].list[^1].SnapLocked()))
			{
				// card must be of same suit and the next number is the sequeunce or is A and slot is empty
				if ((foundations[i].list.Count == 0 && movedCard.GetFace() == 1) || (foundations[i].list.Count != 0 && foundations[i].list[^1].IsSameSuit(movedCard) && foundations[i].list[^1].IsNextFace(movedCard)))
				{
					movedCard.SnapNewPos(foundationSlots[i].GlobalPosition);
					movedCard.GetTopCard()?.SetBottomCard(null);
					movedCard.SetTopCard(null);
					dest = foundations[i];
					snapped = true;
					// shows fireworks if won
					if (CheckWon())
					{
						ShowFireworks();
					}
				}
			}
		}

		// determines if a new score should be added and updates cardSaved List
		if (snapped)
		{
			for (int ii = 0; ii < 4; ii++)
			{
				if (foundations[ii].list.Contains(movedCard))
				{
					skipScore = true;
					break;
				}
			}
			if (!skipScore)
			{
				UpdateScore(10);
			}
			movedCard.ChangeList(dest);
		}

		return snapped;
	}

	// triggered when a successful move is complete. adds move counter and triggers save of current state.
	private void UpdateMoves()
	{
		Label movesLabel = GetNode<Label>("./movesContainer/movesValue");
		movesLabel.Text = (int.Parse(movesLabel.Text) + 1).ToString();

		List<String> newMove = [];

		foreach (CardObject card in everyCard)
		{
			newMove.Add(card.SaveCardData());
		}

		TableMove.Add(new Table(deck, drawnDeck, tabluea, foundations, deckSlot.TextureNormal));

		Moves.Add(newMove);

		Label scoreLabel = GetNode<Label>("./scoreContainer/scoreValue");
		scoreMoves.Add(int.Parse(scoreLabel.Text));
	}

	// triggers when score needs updating
	private void UpdateScore(int score)
	{
		Label scoreLabel = GetNode<Label>("./scoreContainer/scoreValue");
		scoreLabel.Text = (int.Parse(scoreLabel.Text) + score).ToString();
	}

	// triggers when hint button is pressed.
	private void Hint()
	{
		if (gameLoading)
		{
			return;
		}
		if (drawnDeck.list.Count > 0)
		{
			if (HintMove(drawnDeck.list[^1], true))
			{
				return;
			}
		}
		
		foreach (CustomList tableauSlot in tabluea)
		{
			for (int i = tableauSlot.list.Count - 1; i >= 0; i--)
			{
				if (tableauSlot.list[i].IsMovable() && !tableauSlot.list[i].SnapLocked() && HintMove(tableauSlot.list[i]))
				{
					return;
				}
			}
		}
	}

	// logic to trigger a hint move; moves card slowly to applicable spot and snaps back
	private bool HintMove(CardObject card, bool skipHidden = false)
	{
		// returns list of foundation slot if found a available move
		CustomList FoundationSlot = TestFoundation(card);

		// attempts to show a temporary move to a foundation slot if card is not already attempting a snap
		if (FoundationSlot != null)
		{
			for (int i = 0; i < foundations.Length; i++)
			{
				if (foundations[i].list == FoundationSlot.list)
				{
					if (!card.SnapLocked())
					{
						card.TempSnap(foundationSlots[i].GlobalPosition);
						return true;
					}
				}
			}
		}
		else
		{
			// returns list of tableau slot if found a available move
			CustomList TableauSlot = TestTableau(card);

			// attempts to show a temporary move to a tableau slot if card is not already attempting a snap
			if (TableauSlot != null)
			{
				// determines if it should attempt a slot snap or a card snap offset. Determined by card being a K and slot being empty
				if (TableauSlot.list.Count == 0)
				{
					// attempts slot snap
					for (int i = 0; i < tabluea.Length; i++)
					{
						if (TableauSlot.list == tabluea[i].list)
						{
							if (!card.SnapLocked() && (card.HasHiddenBehind() || skipHidden))
							{
								card.TempSnap(tableauSlots[i].GlobalPosition);
								return true;
							}
						}
					}
				}
				else
				{
					// attempts card offset snap
					if (!card.SnapLocked() && (card.HasHiddenBehind()  || skipHidden))
					{
						Vector2 newPos = new(TableauSlot.list[^1].Position.X, TableauSlot.list[^1].Position.Y + 25);
						card.TempSnap(newPos);
						return true;		
					}
				}
			}
		}
		return false;
	}

	// triggers when new game button is pressed; opens new game dialog
	private void NewGame()
	{
		if (gameLoading)
		{
			return;
		}

		var dialog = _newGameDialog.Instantiate<NewGameMessage>();

		AddChild(dialog);

		dialog.MoveToFront();
		dialog.ZIndex = 900;

		Vector2 center = GetViewportRect().Size / 2;
		dialog.GlobalPosition = center;

		dialog.DialogResult += NewGameResult;
		GetTree().Paused = pauseTree;
	}

	// handles yes or no action of new game message dialog
	private void NewGameResult(bool accepted)
	{
		GetTree().Paused = false;
		if (accepted)
		{
			GetTree().ReloadCurrentScene();
		}
		else if (CheckWon())
		{
			GetTree().Quit();
		}
	}

	// Undos last move
	private void Undo()
	{
		if (gameLoading)
		{
			return;
		}

		if (Moves.Count > 1)
		{
			// updates all cards to old positions and variable data
			for (int i = 0; i < everyCard.Count; i++)
			{
				everyCard[i].UpdateCardState(Moves[^2][i], everyCard);
		
				switch(everyCard[i].GetListName())
				{
					case "deck":
						everyCard[i].SetList(deck);
						break;
					case "drawnDeck":
						everyCard[i].SetList(drawnDeck);
						break;
					case "table0":
						everyCard[i].SetList(tabluea[0]);
						break;
					case "table1":
						everyCard[i].SetList(tabluea[1]);
						break;
					case "table2":
						everyCard[i].SetList(tabluea[2]);
						break;
					case "table3":
						everyCard[i].SetList(tabluea[3]);
						break;
					case "table4":
						everyCard[i].SetList(tabluea[4]);
						break;
					case "table5":
						everyCard[i].SetList(tabluea[5]);
						break;
					case "table6":
						everyCard[i].SetList(tabluea[6]);
						break;
					case "foundation0":
						everyCard[i].SetList(foundations[0]);
						break;
					case "foundation1":
						everyCard[i].SetList(foundations[1]);
						break;
					case "foundation2":
						everyCard[i].SetList(foundations[2]);
						break;
					case "foundation3":
						everyCard[i].SetList(foundations[3]);
						break;
				}
			}


			//reset all list arrays to undo last action
			for (int i = 0; i < tabluea.Length; i++)
			{
				tabluea[i].list = [..TableMove[^2].GetTableauAt(i).list];
			}

			for (int i = 0; i < foundations.Length; i++)
			{
				foundations[i].list = [..TableMove[^2].GetFoundationuAt(i).list];
			}

			deck.list = [..TableMove[^2].GetDeck().list];
			drawnDeck.list = [..TableMove[^2].GetDrawnDeck().list];

			// reset deckTexture
			deckSlot.TextureNormal = (Texture2D)TableMove[^2].GetDeckSlotTex();

			//loops through each card in tableau and drawnDeck to have them cascading properly
			for (int i = 0; i < tabluea.Length; i++)
			{
				foreach (CardObject card in tabluea[i].list)
				{
					card.MoveToFront();
				}
			}

			foreach (CardObject card in drawnDeck.list)
			{
				card.MoveToFront();
			}

			if (deck.list.Count > 0)
			{
				deckSlot.MoveToFront();
			}

			
			// move and score counter to go back in time
			Label movesLabel = GetNode<Label>("./movesContainer/movesValue");
			movesLabel.Text = (int.Parse(movesLabel.Text) - 1).ToString();

			Label scoreLabel = GetNode<Label>("./scoreContainer/scoreValue");
			scoreLabel.Text = scoreMoves[^2].ToString();


			// removes turn from list to allow a new turn to take its place
			Moves.RemoveAt(Moves.Count - 1);
			TableMove.RemoveAt(TableMove.Count - 1);
			scoreMoves.RemoveAt(scoreMoves.Count - 1);
		}
	}

	// triggers when Settings button is pressed; opens settings window
	private void OpenSettings()
	{
		if (gameLoading)
		{
			return;
		}

		var dialog = _settingsDialog.Instantiate<SettingsMenu>();

		AddChild(dialog);
		dialog.MoveToFront();
		dialog.ZIndex = 900;

		Vector2 center = GetViewportRect().Size / 2;
		center = new Vector2(center.X, center.Y - 100);
		dialog.GlobalPosition = center;

		dialog.DialogResult += CloseSettings;
		GetTree().Paused = true;
	}

	// triggers when Settings window closes and applies all new settings
	private void CloseSettings()
	{
		foreach (CardObject card in everyCard)
		{
			if (!card.IsFaceUp())
				card.Texture = Settings.cardTemplate;
		}
		if (deckSlot.TextureNormal != emptyDeckImg)
		{
			deckSlot.TextureNormal = Settings.cardTemplate;
		}
		drawMode = Settings.drawMode;
		backColor.Color = new Color(Settings.hexColor);


		Image img = Settings.cardTemplate.GetImage();
		byte[] imgData = img.SavePngToBuffer(); 

		var data = new Godot.Collections.Dictionary
		{
			["texture_buffer"] = imgData
		};

		using var templateFile = FileAccess.Open("user://solitaireTemplate.save", FileAccess.ModeFlags.Write);
		templateFile.StoreVar(data);
		templateFile.Close();

		using var settingsFile = FileAccess.Open("user://solitaireSettings.save", FileAccess.ModeFlags.Write);
		settingsFile.StoreLine(Settings.drawMode.ToString());			
		settingsFile.StoreLine(Settings.hexColor);
		settingsFile.Close();

		GetTree().Paused = false;
	}
}
