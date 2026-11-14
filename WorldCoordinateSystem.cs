
using Godot;

public static class WorldCoordinateSystem
{
	// ==========================================
	// IMAGE PIXEL TO GEOGRAPHICAL COORDINATES
	// ==========================================

	public static Vector2 ImagePixelToLatLon(
		Vector2 pixel,
		Vector2I imageSize)
	{
		return ImagePixelToLatLon(
			pixel,
			new Vector2(imageSize.X, imageSize.Y)
		);
	}

	public static Vector2 ImagePixelToLatLon(
		Vector2 pixel,
		Vector2 imageSize)
	{
		if (imageSize.X <= 0 || imageSize.Y <= 0)
			return Vector2.Zero;

		float x = Mathf.Clamp(
			pixel.X, 0f, imageSize.X - 1f
		);

		float y = Mathf.Clamp(
			pixel.Y, 0f, imageSize.Y - 1f
		);

		float longitude =
			(x / imageSize.X) * 360f - 180f;

		float latitude =
			90f - (y / imageSize.Y) * 180f;

		return new Vector2(longitude, latitude);
	}

	// ==========================================
	// GEOGRAPHICAL COORDINATES TO IMAGE PIXEL
	// ==========================================

	public static Vector2 LatLonToImagePixel(
		float latitude,
		float longitude,
		Vector2I imageSize)
	{
		return LatLonToImagePixel(
			latitude,
			longitude,
			new Vector2(imageSize.X, imageSize.Y)
		);
	}

	public static Vector2 LatLonToImagePixel(
		float latitude,
		float longitude,
		Vector2 imageSize)
	{
		if (imageSize.X <= 0 || imageSize.Y <= 0)
			return Vector2.Zero;

		latitude = Mathf.Clamp(latitude, -90f, 90f);

		longitude =
			((longitude + 180f) % 360f + 360f) % 360f - 180f;

		float x =
			((longitude + 180f) / 360f) * imageSize.X;

		float y =
			((90f - latitude) / 180f) * imageSize.Y;

		return new Vector2(
			Mathf.Clamp(x, 0f, imageSize.X - 1f),
			Mathf.Clamp(y, 0f, imageSize.Y - 1f)
		);
	}
}
