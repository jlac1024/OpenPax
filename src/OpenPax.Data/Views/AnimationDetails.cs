namespace OpenPax.Data.Views;

/// <summary>
/// Class 8037, a looping picture sequence (read). The frame
/// period is 1000 / <see cref="Fps"/> ms; no data has 0, which would divide
/// by zero in the original.
/// </summary>
/// <param name="PictureFile">0 to 9: which of the ten image files holds the frames.</param>
/// <param name="DirectRefresh">1: each new frame goes straight to the screen instead of through the parent.</param>
/// <param name="Preload">1: every frame is loaded before the first play.</param>
public sealed record AnimationDetails(int PictureFile, int FirstPicture, int Frames, byte DirectRefresh, int Fps, byte Preload, byte CacheFrames) : WidgetDetails;
