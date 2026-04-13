using System.Collections.Generic;

// used to save the name of list to make it easier to pass the changed reference to cards upon undo
public class CustomList(List<CardObject> list, string name)
{
    public List<CardObject> list = list;
    public string name = name;
}