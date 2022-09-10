﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace DominusCore {
	public class Renderer : GameWindow {
		// Constants
		/// <summary> Radian Conversion Factor (used for degree-radian conversions). Equal to pi/180. </summary>
		internal const float RCF = 0.017453293f;
		// Multithreading hint
		public static bool IsUpdateComplete = false;

		// Rendering
		public static ShaderProgramGeometry GeometryShader;
		public static ShaderProgramLighting LightingShader;
		public static ShaderProgramInterface InterfaceShader;
		/// <summary> The current render pass, usually set by ShaderProgram.use(). </summary>
		public static RenderPass CurrentPass;

		public static Framebuffer FramebufferGeometry;
		public static Framebuffer DefaultFramebuffer;

		// Content
		private Scene Scene = new Scene();

		// Camera
		private Vector3 CameraPosition = new Vector3(-1.5f, 1.5f, 0f);
		private Vector3 CameraTarget = new Vector3(0f, 0f, 0f);

		// Debugging
		private static DebugProc debugCallback = DebugCallback;
		private static GCHandle debugCallbackHandle;
		private static Stopwatch frameTimer = new Stopwatch();
		/// <summary> Array storing the last n frame lengths, to provide an average in the title bar for performance monitoring. </summary>
		private double[] frameTimes = new double[30];
		private int _debugGroupTracker = 0;

		public Renderer(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws) { }

		/// <summary> The types of render passes that can be done in the render loop, used to control what gets run in Drawable.DrawSelf(). </summary>
		public enum RenderPass {
			Geometry,
			Lighting,
			InterfaceForeground,
			InterfaceBackground,
			InterfaceText
		}

		/// <summary> Handles all graphics setup processing: creates shader program, drawables, sets flags, sets attribs.
		/// <br/> THREAD: OpenGL </summary>
		protected override void OnLoad() {
			// Misc GL flags and callbacks
			debugCallbackHandle = GCHandle.Alloc(debugCallback);
			GL.DebugMessageCallback(debugCallback, IntPtr.Zero);
			GL.Enable(EnableCap.DebugOutput);
			GL.Enable(EnableCap.DebugOutputSynchronous);
			GL.Enable(EnableCap.DepthTest);
			VSync = VSyncMode.On;

			InterfaceShader = new ShaderProgramInterface("src/shaders/InterfaceShader.glsl");
			LightingShader = new ShaderProgramLighting("src/shaders/LightingShader.glsl");
			GeometryShader = new ShaderProgramGeometry("src/shaders/GeometryShader.glsl");

			DefaultFramebuffer = new Framebuffer(0);

			FramebufferGeometry = new Framebuffer();
			FramebufferGeometry.AddDepthBuffer(PixelInternalFormat.DepthComponent24);
			FramebufferGeometry.AddAttachment(PixelInternalFormat.Rgba16f, PixelFormat.Rgba, "GB: gPosition");
			FramebufferGeometry.AddAttachment(PixelInternalFormat.Rgba16f, PixelFormat.Rgba, "GB: gNormal");
			FramebufferGeometry.AddAttachment(PixelInternalFormat.Rgba16f, PixelFormat.Rgba, "GB: gAlbedoSpec");

			FontAtlas.Load("calibri", "assets/fonts/calibri.png", "assets/fonts/calibri.json");

			// Necessary to have this to prevent attribs from crashing due to unbound VBO
			GL.BindBuffer(BufferTarget.ArrayBuffer, GL.GenBuffer());

			InterfaceShader.SetVertexAttribPointers(new[] { 2, 2 });
			GeometryShader.SetVertexAttribPointers(new[] { 3, 2, 4, 3 });

			Console.WriteLine("Renderer.OnLoad() completed");
		}

		/// <summary> Core render loop. <br/> THREAD: OpenGL </summary>
		protected override void OnRenderFrame(FrameEventArgs args) {
			frameTimer.Restart();

			OnUpdateFrame();

			Vector2 ProjectMatrixNearFar = new Vector2(0.01f, 1000000f);
			Matrix4 Perspective3D = Matrix4.CreatePerspectiveFieldOfView(90f * RCF, (float)Size.X / (float)Size.Y, ProjectMatrixNearFar.X, ProjectMatrixNearFar.Y);
			Matrix4 Perspective2D = Matrix4.CreateOrthographicOffCenter(0f, (float)Size.X, 0f, (float)Size.Y, ProjectMatrixNearFar.X, ProjectMatrixNearFar.Y);
			List<Light> listSceneLights = Scene.Geometry.GetAllChildrenOfType<Light>();

			BeginPass("G-Buffer");
			FramebufferGeometry.Use().Reset();
			GeometryShader.Use(RenderPass.Geometry);
			Matrix4 MatrixView = Matrix4.LookAt(CameraPosition, CameraPosition + CameraTarget, Vector3.UnitY);
			GL.UniformMatrix4(GeometryShader.UniformView_ID, true, ref MatrixView);
			GL.UniformMatrix4(GeometryShader.UniformPerspective_ID, true, ref Perspective3D);
			int drawcalls = Scene.Geometry.Draw();
			EndPass();

			BeginPass("Lighting");
			DefaultFramebuffer.Use().Reset();
			LightingShader.Use(RenderPass.Lighting);
			FramebufferGeometry.GetAttachment(0).Bind(0);
			FramebufferGeometry.GetAttachment(1).Bind(1);
			FramebufferGeometry.GetAttachment(2).Bind(2);
			GL.Uniform3(LightingShader.UniformCameraPosition_ID, CameraPosition.X, CameraPosition.Y, CameraPosition.Z);
			LightingShader.SetLightSSBO(listSceneLights);
			GL.DrawArrays(OpenTK.Graphics.OpenGL4.PrimitiveType.Triangles, 0, 3);
			EndPass();

			BeginPass("Interface");
			// Copy geometry depth to default framebuffer (world space -> screen space)
			DefaultFramebuffer.BlitFrom(FramebufferGeometry, ClearBufferMask.DepthBufferBit);
			InterfaceShader.Use(RenderPass.InterfaceBackground);
			GL.UniformMatrix4(InterfaceShader.UniformPerspective_ID, true, ref Perspective2D);
			drawcalls += Scene.Interface.Draw();
			InterfaceShader.Use(RenderPass.InterfaceForeground);
			drawcalls += Scene.Interface.Draw();
			InterfaceShader.Use(RenderPass.InterfaceText);
			drawcalls += Scene.Interface.Draw();
			EndPass();

			// Frame done
			Context.SwapBuffers();
			frameTimer.Stop();

			// How long did the frame take?
			Array.Copy(frameTimes, 1, frameTimes, 0, frameTimes.Length - 1);
			frameTimes[frameTimes.Length - 1] = 1000f * frameTimer.ElapsedTicks / Stopwatch.Frequency;
			double time = frameTimes.Sum() / frameTimes.Length;
			double goal = 1000 / 60;
			this.Title = $"Display - FPS: {1000 / time,-1:F1} Drawcalls: {drawcalls} Lights: {listSceneLights.Count()} Frametime: {time,-4:F2}ms, Budget: {time / goal,-2:P2} (target: {goal}ms)";
			_debugGroupTracker = 0;
		}

		private void OnUpdateFrame() {

		}

		/// <summary> Handles resizing and keeping GLViewport correct</summary>
		protected override void OnResize(ResizeEventArgs e) {
			GL.Viewport(0, 0, Size.X, Size.Y);
		}

		/// <summary> Handles all debug callbacks from OpenGL and throws exceptions if unhandled. </summary>
		private static void DebugCallback(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam) {
			string messageString = Marshal.PtrToStringAnsi(message, length);
			if (type < DebugType.DebugTypeOther)
				Console.WriteLine($"{severity} {type} | {messageString}");
			if (type == DebugType.DebugTypeError)
				throw new Exception(messageString);
		}

		/// <summary> Starts a GPU debug group, used for grouping operations together into one section for debugging in RenderDoc. </summary>
		private void BeginPass(string title) {
			GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, _debugGroupTracker++, title.Length, title);
		}

		/// <summary> Ends the current debug group on the GPU. </summary>
		private void EndPass() {
			GL.PopDebugGroup();
		}

		/// <summary> Assigns a debug label to a specified GL object - useful in debugging tools similar to debug groups. </summary>
		private static void DebugLabel(ObjectLabelIdentifier type, int id, string label) {
			GL.ObjectLabel(type, id, label.Length, label);
		}
	}

	public class Program {
		public static Renderer Renderer;

		public static void Main(string[] args) {
			Assimp.Unmanaged.AssimpLibrary.Instance.LoadLibrary();

			using (Renderer g = new Renderer(new GameWindowSettings(), new NativeWindowSettings() {
				Size = new Vector2i(1600, 900),
				Title = "Display",
				WindowBorder = WindowBorder.Fixed
			})) {
				Renderer = g;
				g.Run();
			}
		}

		public static void Crash(string error) {
			Crash(new Exception(error));
		}

		public static void Crash(Exception e) {
			Console.WriteLine(e.ToString());
			Exit(-1);
		}

		public static void Exit() {
			Exit(0);
		}

		public static void Exit(int error) {
			System.Environment.Exit(error);
		}

	}
}
