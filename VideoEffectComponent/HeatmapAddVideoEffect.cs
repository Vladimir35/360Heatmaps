﻿using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Foundation.Collections;
using Windows.Graphics.DirectX.Direct3D11;
using System.Runtime.InteropServices;
using Microsoft.Graphics.Canvas.Effects;
using Windows.UI;
using Windows.UI.Xaml.Media.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using System.Diagnostics;
using BivrostHeatmapViewer;
using System.Collections;

namespace VideoEffectComponent
{

	[ComImport]
	[Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	unsafe interface IMemoryBufferByteAccess
	{
		void GetBuffer(out byte* buffer, out uint capacity);
	}


	public sealed class HeatmapAddVideoEffect : IBasicVideoEffect
	{
		//public static IDictionary<string, List<int>> coords;

		public HeatmapAddVideoEffect()
		{
			Debug.WriteLine("ExampleVideoEffect constructor");
		}

		public MediaMemoryTypes SupportedMemoryTypes { get { return MediaMemoryTypes.Gpu; } }
		public bool TimeIndependent { get { return false; } }
		public void Close(MediaEffectClosedReason reason)
		{
			// Dispose of effect resources
		}

		private int frameCount;
		public void DiscardQueuedFrames()
		{
			frameCount = 0;
		}

		public bool IsReadOnly { get { return false; } }

		private CanvasDevice canvasDevice;
		public void SetEncodingProperties(VideoEncodingProperties encodingProperties, IDirect3DDevice device)
		{
			canvasDevice = CanvasDevice.CreateFromDirect3D11Device(device);
		}

		public IReadOnlyList<VideoEncodingProperties> SupportedEncodingProperties
		{
			get
			{
				var encodingProperties = new VideoEncodingProperties();
				encodingProperties.Subtype = "ARGB32";
				return new List<VideoEncodingProperties>() { encodingProperties };

				// If the list is empty, the encoding type will be ARGB32.
				// return new List<VideoEncodingProperties>();
			}
		}

		private IPropertySet configuration;
		public void SetProperties(IPropertySet configuration)
		{
			this.configuration = configuration;
			//this.pitch = new List<int>();
			//this.yaw = new List<int>();
			//this.fov = new List<int>();

			object count;
			if (configuration.TryGetValue("count", out count))
			{
				this.count = (int)count;

				object pitch;
				configuration.TryGetValue("pitch", out pitch);

				object yaw;
				configuration.TryGetValue("yaw", out yaw);

				object fov;
				configuration.TryGetValue("fov", out fov);

				this.pitch = pitch as List<int>;
				this.yaw = yaw as List<int>;
				this.fov = fov as List<int>;

			}
		}

		private List<int> pitch;
		private List<int> yaw;
		private List<int> fov;
		private int count;

		private double frameLength = 0;

		public void ProcessFrame(ProcessVideoFrameContext context)
		{

			using (CanvasBitmap inputBitmap = CanvasBitmap.CreateFromDirect3D11Surface(canvasDevice, context.InputFrame.Direct3DSurface))
			using (CanvasRenderTarget renderTarget = CanvasRenderTarget.CreateFromDirect3D11Surface(canvasDevice, context.OutputFrame.Direct3DSurface))
			using (CanvasDrawingSession ds = renderTarget.CreateDrawingSession())
			using (var scaleEffect = new ScaleEffect())
			{
				if (frameLength == 0)
				{
					frameLength = context.InputFrame.Duration.Value.TotalMilliseconds;
				}


				//double dur = context.InputFrame.Duration.Value.TotalMilliseconds;
				double rel = context.InputFrame.RelativeTime.Value.TotalMilliseconds;

				int frameTimeCounter = (int)Math.Round(rel / frameLength) -1;

				Debug.WriteLine("Frame: " + frameTimeCounter);

                int[] pitch = new int[count];
                int[] yaw = new int[count];
                int[] fov = new int[count];

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        pitch[i] = this.pitch[frameTimeCounter + i];
                        fov[i] = this.fov[frameTimeCounter + i];
                        yaw[i] = this.yaw[frameTimeCounter + i];
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        Debug.WriteLine(ex.Message);
                        pitch[i] = 0;
                        fov[i] = 0;
                        yaw[i] = 0;
                    }
                }

				byte[] tab = Heatmap.GenerateHeatmap(pitch, yaw, fov);
				CanvasBitmap cb = CanvasBitmap.CreateFromBytes(canvasDevice, tab, 64, 64, Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, 96, CanvasAlphaMode.Premultiplied);
				scaleEffect.Source = cb;
				scaleEffect.Scale = new System.Numerics.Vector2(3840 / 64, 2160 / 64);
				scaleEffect.InterpolationMode = CanvasImageInterpolation.Linear;
				scaleEffect.BorderMode = EffectBorderMode.Hard;
				ds.DrawImage(inputBitmap);
				ds.DrawImage(scaleEffect, 0, 120, new Windows.Foundation.Rect { Height = 2160, Width = 3840 }, 0.35f);
				ds.Flush();
			}


			//Debug.Write(dur + "ms ===> ");
			//Debug.WriteLine(rel + "ms ");			

		}
	}

}
