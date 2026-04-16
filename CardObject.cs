using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;

public partial class CardObject : Sprite2D
{
	private bool gameLoading = true;
	private readonly Texture fullCardsImg = (Texture) GD.Load("res://fotos/Full_Deck.png");
	private Vector2 orgFingerPos, orgCardPos, snapPos = new(0, 0), tempSnap = new (0, 0);
	private CardObject bottomCard, topCard;
	private CustomList currentList;
	private Rect2 imgRegion;
	private bool moving = false, movable = false, IsListDrawnDeck = false, snapLocked = false, FaceUp = true, oneClick = false, waitFlip = false;
	private int suit, face;
	float snapSpeed = 10f, timeElapsed = 0f;
	private string listName = "";

	[Signal]
	public delegate void CardMovedEventHandler(CardObject card, Vector2 orgPos); //signal used when card attempted to move
	[Signal]
	public delegate void CardDoubleClickedEventHandler(CardObject card); // signal when card attempted an auto move
	[Signal]
	public delegate void ScoreChangeEventHandler(int score); // signal when move triggers a score change

	// helps initalize object with correct name, face and texture
	public CardObject Init(int suit, int face)
	{
		this.suit = suit;
		this.face = face;
		int offset = 2;

		int suitLoc = (suit - 1) * 96; 
		int faceLoc = (face - 1) * 71;

		if (suitLoc > 0)
		{
			suitLoc += offset * (suit -1);
		}
		if (faceLoc > 0)
		{
			faceLoc += offset * (face - 1);
		}
		Name = suit + " " + face;
		imgRegion =  new Rect2(faceLoc, suitLoc, 71.0f, 96.0f);
		this.RegionRect = imgRegion;
		return this;
	}

	public int GetSuit()
	{
		return suit;
	}

	public int GetFace()
	{
		return face;
	}

	public void SetBottomCard(CardObject card)
	{
		this.bottomCard = card;
	}

	public CardObject GetBottomCard()
	{
		return bottomCard;
	}

	public void SetTopCard(CardObject card)
	{
		topCard = card;
		card?.SetBottomCard(this);
	}

	public CardObject GetTopCard()
	{
		return topCard;
	}

	public void SetMovable(bool movable)
	{
		this.movable = movable;
	}

	public bool IsMovable()
	{
		return movable;
	}

	public bool SnapLocked()
	{
		if (snapLocked)
		{
			return true;
		}
		else if (bottomCard != null)
		{
			return bottomCard.SnapLocked();
		}
		else
		{
			return false;
		}
	}

	public bool IsFaceUp()
	{
		return FaceUp;
	}

	public void setGameLoaded()
	{
		gameLoading = false;
	}

	public bool IsOppositeSuit(CardObject card)
	{
		if (card.GetSuit() % 2 != this.suit % 2)
		{
			return true;
		}
		return false;
	}

	public bool IsSameSuit(CardObject card)
	{
		if (card.GetSuit() == suit)
		{
			return true;
		}
		return false;
	}

	public bool IsNextFace(CardObject card)
	{
		if (card.GetFace() == (face + 1))
		{
			return true;
		}
		return false;
	}

	public bool IsPrevFace(CardObject card)
	{
		if (card.GetFace() == (face - 1))
		{
			return true;
		}
		return false;
	}

	public bool HasHiddenBehind()
	{
		if (currentList != null)
		{
			int index = currentList.list.IndexOf(this);
			if (index != 0)
			{
				return !currentList.list[index -1].IsFaceUp();
			}
		}
		return false;
	}

	public void FlipCard(bool waitOnFlip = false)
	{
		if (waitOnFlip && snapLocked)
		{
			waitFlip = waitOnFlip;
			return;
		}
		if (FaceUp)
		{
			this.Texture = (Texture2D)Settings.cardTemplate;
			this.RegionRect = new Rect2(0,0,71,96);
		}
		else
		{
			this.Texture = (Texture2D)fullCardsImg;
			this.RegionRect = imgRegion;
		}
		FaceUp = !FaceUp;
	}

	public void BeginMove()
	{
		orgCardPos = Position;
		this.bottomCard?.BeginMove();
	}

	public void Move(Vector2 fingerPos, Vector2 orgFingerPos)
	{
		Vector2 newPos;
		newPos = Position;
		newPos.X += fingerPos.X - orgFingerPos.X;
		newPos.Y += fingerPos.Y - orgFingerPos.Y;
		Position = newPos;
		MoveToFront();
		bottomCard?.Move(fingerPos, orgFingerPos);
	}

	public void ReturnToOrigin()
	{
		snapPos = orgCardPos;
		snapLocked = true;
		MoveToFront();
		bottomCard?.ReturnToOrigin();
	}

	public async void ChangeList(CustomList newList, bool movable = true, bool fromDrawn = false)
	{
		if (currentList != null)
		{
			int index = currentList.list.IndexOf(this);
			if (index != 0)
			{
				if (!currentList.list[index -1].IsFaceUp())
				{
					//await Task.Delay(100);
					currentList.list[index -1].FlipCard();
					EmitSignal(SignalName.ScoreChange, 5);
				}
				currentList.list[index -1].SetMovable(movable);
				if (IsListDrawnDeck && currentList.list.Count > 3)
				{
					if (Settings.drawMode == 3)
					{
						currentList.list[index-1].SnapNewPos(new Vector2(414 + (2 * 25), currentList.list[index-1].Position.Y));
						currentList.list[index-2].SnapNewPos(new Vector2(414 + (1 * 25), currentList.list[index-2].Position.Y));
						currentList.list[index-3].SnapNewPos(new Vector2(414 + (0 * 25), currentList.list[index-3].Position.Y));
					}
				}
			}
		}
		currentList?.list.Remove(this);
		currentList = newList;
		currentList.list.Add(this);
		bottomCard?.ChangeList(newList);
		IsListDrawnDeck = fromDrawn;
	}

	public void SnapNewPos(Vector2 newPos)
	{
		snapPos = newPos;
		newPos = new Vector2(newPos.X, newPos.Y + 25);
		snapLocked = true;
		bottomCard?.SnapNewPos(newPos);
	}

	public void TempSnap(Vector2 newPos)
	{
		MoveToFront();
		snapSpeed = 7f;
		orgCardPos = Position;
		tempSnap = newPos;
		newPos = new Vector2(newPos.X, newPos.Y + 25);
		snapLocked = true;
		bottomCard?.TempSnap(newPos);
	}

	public string SaveCardData()
	{
		Vector2 ToSave;
		bool faceSave = FaceUp;
		if (snapPos != new Vector2(0, 0))
		{
			ToSave = snapPos;
		}
		else if (tempSnap != new Vector2(0, 0))
		{
			ToSave = orgCardPos;
		}
		else
		{
			ToSave = Position;
		}

		if (waitFlip)
		{
			faceSave = !faceSave;
		}

		var data = new Dictionary
		{
			{"position", new Vector2(ToSave.X, ToSave. Y)},
			{"bottomCard", bottomCard?.Name},
			{"topCard", topCard?.Name},
			{"IsFaceUp", faceSave},
			{"movable", movable},
			{"listName", currentList.name}
		};

		return Json.Stringify(data);
	}

	public void UpdateCardState(string dict, List<CardObject> allCards)
	{
		var varData = Json.ParseString(dict);
		Dictionary data = (Dictionary)varData;

		Position = (Vector2)GD.StrToVar("Vector2" + data["position"].AsString());

		FaceUp = data["IsFaceUp"].AsBool();

		if (data["IsFaceUp"].AsBool())
		{
			Texture = (Texture2D)fullCardsImg;
			RegionRect = imgRegion;
		}
		else
		{
			Texture = (Texture2D)Settings.cardTemplate;
			RegionRect = new Rect2(0,0,71,96);
		}

		if (data["bottomCard"].AsString() == "")
		{
			bottomCard = null;
		}

		if (data["bottomCard"].AsString() == "")
		{
			topCard = null;
		}

		movable = data["movable"].AsBool();

		listName = data["listName"].AsString();

		foreach (CardObject card in allCards)
		{
			if (card.Name == data["bottomCard"].AsString())
			{
				bottomCard = card;
			}
			else if (card.Name == data["topCard"].AsString())
			{
				topCard = card;
			}
		}
	}

	public string GetListName()
	{
		return listName;
	}

	public void SetList(CustomList list)
	{
		currentList = list;
	}

	// occurs when any input is detected
	// used to detect if card was actually touched 
	// consumes event to prevent others from moving
	public override void _Input(InputEvent @event)
	{
		if (gameLoading)
		{
			GetViewport().SetInputAsHandled();
			return;
		}
		float cardWidth = 71 * Scale.X, cardHeight = 96 * Scale.Y;

		if (@event is InputEventScreenTouch && @event.IsPressed()  && Finger_Index(@event) == 0)
		{
			Vector2 cardEnd = new(Position.X + cardWidth, Position.Y + cardHeight);
			Vector2 fingerPos = Finger_Position(@event);

			if ((fingerPos.X >= Position.X && fingerPos.X <= cardEnd.X) && (fingerPos.Y >= Position.Y && fingerPos.Y <= cardEnd.Y) && movable && !snapLocked && (bottomCard == null || !bottomCard.SnapLocked()))
			{
				// oneClick used to determine if double clicked
				if (!oneClick)
				{
					oneClick = true;
					timeElapsed = 0f;
					moving = true;
					orgFingerPos = fingerPos;
					BeginMove();
					GetViewport().SetInputAsHandled();
				}
				else
				{
					oneClick = false;
					EmitSignal(SignalName.CardDoubleClicked, this);
					GetViewport().SetInputAsHandled();
				}
			}
		}
		if (@event is InputEventScreenDrag  && Finger_Index(@event) == 0)
		{
			Vector2 fingerPos = Finger_Position(@event);
			if (moving)
			{
				Move(fingerPos, orgFingerPos);
				orgFingerPos = fingerPos;
			}
		}
		if (@event is InputEventScreenTouch && !@event.IsPressed()  && Finger_Index(@event) == 0)
		{
			if (moving)
			{
				moving = false;

				EmitSignal(SignalName.CardMoved, this, orgCardPos);

			}
		}
	}

	// occurs when screen is redrawn
	// used to disbable double click logic if time elapsed
	// also used to slowly move card towards its goal for more seamless moving
	public override void _Process(double delta)
	{

		if (oneClick)
		{
			timeElapsed += (float) delta;

			if (timeElapsed >= 0.5)
			{
				oneClick = false;
			}
		}
		if (snapPos != new Vector2(0, 0))
		{
			Position = Position.MoveToward(snapPos, snapSpeed);

			snapSpeed *= 1.1f;
			
			if (Position == snapPos)
			{
				snapLocked = false;
				snapPos = new(0, 0);
				snapSpeed = 10f;
				if (waitFlip)
				{
					FlipCard();
					waitFlip = false;
				}
			}
		}
		if (tempSnap != new Vector2(0, 0))
		{
			Position = Position.MoveToward(tempSnap, snapSpeed);
			
			if (Position == tempSnap)
			{
				snapLocked = false;
				tempSnap = new(0, 0);
				snapSpeed = 10f;
				Position = orgCardPos;
				if (waitFlip)
				{
					FlipCard();
					waitFlip = false;
				}
			}
		}
	}

	// determines finger index
	public static int Finger_Index(InputEvent @event)
  	{
		int fingerI = -1;
		if (@event is InputEventScreenDrag)
		{
			var finger = ((InputEventScreenDrag)@event);
			fingerI = finger.Index;
		}
		else
		{
			var finger = ((InputEventScreenTouch)@event);
			fingerI = finger.Index;
		}
		return fingerI;
  	}

	// determines finger position
	public static Vector2 Finger_Position(InputEvent @event)
  	{
		Vector2 fingerPos = Vector2.Zero;
		if (@event is InputEventScreenDrag)
		{
			var finger = ((InputEventScreenDrag)@event);
			fingerPos = finger.Position;
		}
		else
		{
			var finger = ((InputEventScreenTouch)@event);
			fingerPos = finger.Position;
		}
		return fingerPos;
  	}
}
