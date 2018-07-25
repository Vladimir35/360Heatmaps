﻿using Microsoft.Graphics.Canvas;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace BivrostHeatmapViewer
{



	public class StaticHeatmapGenerator
	{
		private static async  Task<WriteableBitmap> GenerateHeatmap(Session session, bool forceFov, int forcedFov)
		{
			var deserializedData = Heatmap.CoordsDeserialize(session.history);

			if (forceFov)
			{
				foreach (Heatmap.Coord h in deserializedData)
				{
					h.fov = forcedFov;
				}
			}

			float[] heatmap = await Task.Factory.StartNew<float[]>(() =>
				Heatmap.Generate(deserializedData)
			);

			var renderedHeatmap = Heatmap.RenderHeatmap(heatmap);

			WriteableBitmap wb = new WriteableBitmap(64, 64);


			using (Stream stream = wb.PixelBuffer.AsStream())
			{
				await stream.WriteAsync(renderedHeatmap, 0, renderedHeatmap.Length);
			}

			return wb;
		}

		public static async Task<MediaStreamSource> GenerateHeatmap(bool forceFov, int forcedFov, bool horizonFlag, SessionCollection sessions, Rect overlayPosition, Windows.UI.Color colorPickerColor, double heatmapOpacity)
		{

			CheckHistoryErrors(sessions);

			StringBuilder sb = new StringBuilder();
			MediaOverlayLayer mediaOverlayLayer = new MediaOverlayLayer();
			WriteableBitmap wb;

			foreach (Session x in sessions.sessions)
			{
				sb.Append(x.history);
			}

			Session s = new Session();
			s.history = sb.ToString();

			wb = await GenerateHeatmap(s, forceFov, forcedFov);

			CanvasDevice device = CanvasDevice.GetSharedDevice();

			SoftwareBitmap swb = SoftwareBitmap.CreateCopyFromBuffer(wb.PixelBuffer, BitmapPixelFormat.Bgra8, wb.PixelWidth, wb.PixelHeight);
			swb = SoftwareBitmap.Convert(swb, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);

			CanvasBitmap canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(device, swb);

			var clip = MediaClip.CreateFromSurface(canvasBitmap, new TimeSpan(0, 0, 0, 0, 1));

			MediaOverlay mediaOverlay = new MediaOverlay(clip);
			mediaOverlay.Position = overlayPosition;
			mediaOverlay.Opacity = heatmapOpacity;

			mediaOverlayLayer.Overlays.Add(mediaOverlay);


			if (horizonFlag)
			{
				CanvasBitmap cb = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), new Uri("ms-appx:///Assets/horizon3840x2160.png"));

				MediaOverlay horizonOverlay = new MediaOverlay(MediaClip.CreateFromSurface(cb, new TimeSpan(0, 0, 0, 0, 1))); 
				horizonOverlay.Position = overlayPosition;
				horizonOverlay.Opacity = 1;


				mediaOverlayLayer.Overlays.Add(horizonOverlay);

			}



			MediaComposition mediaComposition = new MediaComposition();

			mediaComposition.Clips.Add(MediaClip.CreateFromColor(colorPickerColor, new TimeSpan(0, 0, 0, 0, 1)));
			mediaComposition.OverlayLayers.Add(mediaOverlayLayer);

			return mediaComposition.GeneratePreviewMediaStreamSource
				(
				(int)overlayPosition.Width,
				(int)overlayPosition.Height
				);

		}

		public static void RenderCompositionToFile(StorageFile file, MediaComposition composition, saveProgressCallback ShowErrorMessage, Window window, MediaEncodingProfile encodingProfile, CancellationToken token, object selectedResolution)
		{

			var saveOperation = composition.RenderToFileAsync(file, MediaTrimmingPreference.Precise, encodingProfile);

			saveOperation.Progress = new AsyncOperationProgressHandler<TranscodeFailureReason, double>(async (info, progress) =>
			{
				await window.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
				{
					ShowErrorMessage(progress);
					try
					{
						if (token.IsCancellationRequested)
						{
							saveOperation.Cancel();
							ShowErrorMessage(100.0);
						}
					}
					catch (OperationCanceledException)
					{
					}
				}));
			});

			saveOperation.Completed = new AsyncOperationWithProgressCompletedHandler<TranscodeFailureReason, double>(async (info, status) =>
				{
					await window.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
					{
						if (saveOperation.Status != AsyncStatus.Canceled)
						{
							try
							{
								var results = info.GetResults();
								if (results != TranscodeFailureReason.None || status != AsyncStatus.Completed)
								{
									//ShowErrorMessage("Saving was unsuccessful");
								}
								else
								{
									//ShowErrorMessage("Trimmed clip saved to file");
								}
							}
							catch (Exception e)
							{
								Debug.WriteLine("Saving exception: " + e.Message);
                                ShowErrorMessage(100.0);
							}
							finally
							{
								// Update UI whether the operation succeeded or not
							}
						}
					}));
				});
		}
		
		public static async void CheckHistoryErrors (SessionCollection sessions)
		{
			bool flag = false;

			foreach (Session s in sessions.sessions)
			{
				if (s.history.Contains("--"))
				{
					flag = true;
					break;
				}
				else
				{
					flag = false;
				}
			}

			if (flag)
			{
				var dialog = new MessageDialog("Sessions contains time errors. Added empty heatmaps to repair it.");
				dialog.Title = "Warning";
				dialog.Commands.Add(new UICommand { Label = "OK", Id = 0 });
				await dialog.ShowAsync();
			}
		}

	}

}
