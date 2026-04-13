using Godot;
using System;
using System.Collections.Generic;

// class used to help save complex data for easier undo function
public class Table
{
    private readonly CustomList deck, drawnDeck;
	private readonly CustomList[] tabluea = new CustomList[7];
	private readonly CustomList[] foundations = new CustomList[4];
    private readonly Texture deckSlotTexture;

    public Table(CustomList deck, CustomList drawnDeck, CustomList[] tabluea, CustomList[] foundations, Texture deckSlotTexture)
    {
        DoTableau(tabluea);
        DoFoundations(foundations);
        this.deck = new([..deck.list], "deck");
        this.drawnDeck = new([..drawnDeck.list], "drawnDeck");
        this.deckSlotTexture = deckSlotTexture;
    }
    
    private void DoTableau(CustomList[] tablueaList)
    {
        for (int i = 0; i < tablueaList.Length; i++)
        {
            tabluea[i] = new([..tablueaList[i].list], "table" + i);
        }
    }

    private void DoFoundations( CustomList[] foundationList)
    {
        for (int i = 0; i < foundationList.Length; i++)
        {
            foundations[i] =  new([..foundationList[i].list], "foundation" + i);
        }
    }

    public CustomList GetTableauAt(int index)
    {
        return tabluea[index];
    }

    public CustomList GetFoundationuAt(int index)
    {
        return foundations[index];
    }

    public CustomList GetDeck()
    {
        return deck;
    }

    public CustomList GetDrawnDeck()
    {
        return drawnDeck;
    }

    public Texture GetDeckSlotTex()
    {
        return deckSlotTexture;
    }
}