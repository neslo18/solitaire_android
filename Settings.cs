using Godot;

public static class Settings
{
    public static int drawMode;

    public static string hexColor;
    
    public static Texture2D cardTemplate;

    public static Image AddBackground(Image foreground)
{
    // 1. Load the background
    Image background = GD.Load<Texture2D>("res://fotos/card_background.png").GetImage();

    // 2. Create the canvas (Ensure it uses RGBA8 for transparency support)
    Image finalImage = Image.Create(71, 96, false, Image.Format.Rgba8);
    
    // 3. Ensure the foreground matches the canvas format to avoid errors
    if (foreground.GetFormat() != Image.Format.Rgba8)
    {
        foreground.Convert(Image.Format.Rgba8);
    }

    // 4. Draw the background onto the canvas
    // We use BlitRect here because the background is the base layer
    finalImage.BlitRect(background, new Rect2I(Vector2I.Zero, background.GetSize()), Vector2I.Zero);

    // 5. Calculate Centering
    // Foreground is 63x88, Canvas is 71x96
    int offsetX = (71 - 67) / 2;
    int offsetY = (96 - 92) / 2;

    // 6. Layer the foreground on top
    // Use BlendRect so that the foreground's alpha channel is respected
    Rect2I srcRect = new Rect2I(0, 0, 67, 92);
    finalImage.BlendRect(foreground, srcRect, new Vector2I(offsetX, offsetY));

    return finalImage;
}
}