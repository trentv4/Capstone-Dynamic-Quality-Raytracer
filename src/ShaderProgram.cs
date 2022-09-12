using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenTK.Graphics.OpenGL4;

namespace DominusCore {
	/// <summary> Superclass for all shaderprograms. Handles attachment and cleanup of individual shaders. </summary>
	public class ShaderProgram {
		/// <summary> OpenGL ID for this shaderprogram. </summary>
		public int ShaderProgram_ID { get; private set; } = -1;
		private readonly int _vertexArrayObject_ID;
		private Shader[] _shaders;

		public ShaderProgram(string unified) {
			_vertexArrayObject_ID = GL.GenVertexArray();
			_shaders = new Shader[] {
				new Shader(unified, ShaderType.VertexShader, true),
				new Shader(unified, ShaderType.FragmentShader, true)
			};
			ShaderProgram_ID = GL.CreateProgram();
			GL.AttachShader(ShaderProgram_ID, _shaders[0].shaderID);
			GL.AttachShader(ShaderProgram_ID, _shaders[1].shaderID);
			TryLoadShaders();
			SetOneTimeUniforms();
		}

		private void TryLoadShaders() {
			bool isAnyShaderReloaded = false;
			foreach (Shader s in _shaders) {
				if (s.TryLoad()) isAnyShaderReloaded = true;
			}
			if (isAnyShaderReloaded) {
				GL.LinkProgram(ShaderProgram_ID);
				if (GL.GetProgramInfoLog(ShaderProgram_ID) != System.String.Empty) {
					Console.WriteLine($"Error linking shader program: {GL.GetProgramInfoLog(ShaderProgram_ID)}");
				}
				GL.UseProgram(ShaderProgram_ID);
			}

			SetUniforms();
		}

		public virtual ShaderProgram Use(Renderer.RenderPass pass) {
			TryLoadShaders();
			GL.UseProgram(ShaderProgram_ID);
			GL.BindVertexArray(_vertexArrayObject_ID);
			Renderer.CurrentPass = pass;
			return this;
		}

		/// <summary> Assigns the pre-determined vertex attrib information to attrib pointers. This is called once after
		/// creating at least one VBO in this format. Provide the attribs as a series of ints specifying attrib size.
		/// For example, [vec3, vec4, vec3, vec2] would be int[] { 3, 4, 3, 2 }. </summary>
		public virtual ShaderProgram SetVertexAttribPointers(int[] attribs) {
			Use(Renderer.CurrentPass);
			int stride = attribs.Sum() * sizeof(float);
			int runningTotal = 0;
			for (int i = 0; i < attribs.Length; i++) {
				GL.EnableVertexAttribArray(i);
				GL.VertexAttribPointer(i, attribs[i], VertexAttribPointerType.Float, false, stride, runningTotal);
				runningTotal += attribs[i] * sizeof(float);
			}
			return this;
		}

		protected virtual void SetUniforms() { }

		protected virtual void SetOneTimeUniforms() { }

		private class Shader {
			internal readonly string filePath;
			internal DateTime lastWriteTime;
			internal int shaderID;
			internal bool isUnified;
			internal ShaderType type;

			internal Shader(string filePath, ShaderType type, bool isUnified) {
				this.filePath = filePath;
				this.lastWriteTime = DateTime.UnixEpoch;
				this.shaderID = GL.CreateShader(type);
				this.isUnified = isUnified;
				this.type = type;
			}

			internal bool TryLoad() {
				DateTime updatedLastTime = File.GetLastWriteTime(filePath);
				if (lastWriteTime == updatedLastTime)
					return false;
				lastWriteTime = updatedLastTime;

				string shaderSource = "";
				if (isUnified) {
					string[] tempSources = new StreamReader(filePath).ReadToEnd().Split("<split>");
					if (type == ShaderType.VertexShader) shaderSource = tempSources[0];
					if (type == ShaderType.FragmentShader) shaderSource = tempSources[1];
				} else {
					shaderSource = new StreamReader(filePath).ReadToEnd();
				}

				GL.ShaderSource(shaderID, shaderSource);
				GL.CompileShader(shaderID);

				if (GL.GetShaderInfoLog(shaderID) != System.String.Empty) {
					Console.WriteLine($"Error compiling shader {filePath}: {GL.GetShaderInfoLog(shaderID)}");
				}
				return true;
			}
		}
	}

	/// <summary> Interface shader program, with extra uniform IDs only needed for interface shaders. </summary>
	public class ShaderProgramInterface : ShaderProgram {
		public ShaderProgramInterface(string unifiedPath) : base(unifiedPath) { }

		public int UniformElementTexture_ID { get; private set; } = -1;
		public int UniformModel_ID { get; private set; } = -1;
		public int UniformDepth_ID { get; private set; } = -1;
		public int UniformPerspective_ID { get; private set; } = -1;
		public int UniformIsFont_ID { get; private set; } = -1;

		protected override void SetUniforms() {
			UniformElementTexture_ID = GL.GetUniformLocation(ShaderProgram_ID, "elementTexture");
			UniformModel_ID = GL.GetUniformLocation(ShaderProgram_ID, "model");
			UniformDepth_ID = GL.GetUniformLocation(ShaderProgram_ID, "depth");
			UniformPerspective_ID = GL.GetUniformLocation(ShaderProgram_ID, "perspective");
			UniformIsFont_ID = GL.GetUniformLocation(ShaderProgram_ID, "isFont");
		}

		/// <summary> Sets OpenGL to use this shader program, and keeps track of the current shader in Game. </summary>
		public override ShaderProgramInterface Use(Renderer.RenderPass pass) {
			base.Use(pass);

			if (pass == Renderer.RenderPass.InterfaceBackground) {
				GL.Uniform1(UniformDepth_ID, 0.999999f);
				GL.Uniform1(UniformIsFont_ID, 0);
			} else if (pass == Renderer.RenderPass.InterfaceForeground) {
				GL.Uniform1(UniformDepth_ID, 0.2f);
				GL.Uniform1(UniformIsFont_ID, 0);
			} else if (pass == Renderer.RenderPass.InterfaceText) {
				GL.Uniform1(UniformDepth_ID, 0.1f);
				GL.Uniform1(UniformIsFont_ID, 1);
			}

			return this;
		}
	}

	/// <summary> Geometry shader program, with extra uniform IDs only needed for geometry shaders. </summary>
	public class ShaderProgramGeometry : ShaderProgram {
		public ShaderProgramGeometry(string unifiedPath) : base(unifiedPath) { }

		public int[] TextureUniforms { get; private set; }
		public int UniformModel_ID { get; private set; } = -1;
		public int UniformView_ID { get; private set; } = -1;
		public int UniformPerspective_ID { get; private set; } = -1;

		protected override void SetUniforms() {
			UniformModel_ID = GL.GetUniformLocation(ShaderProgram_ID, "model");
			UniformView_ID = GL.GetUniformLocation(ShaderProgram_ID, "view");
			UniformPerspective_ID = GL.GetUniformLocation(ShaderProgram_ID, "perspective");

			TextureUniforms = new int[] {
				GL.GetUniformLocation(ShaderProgram_ID, "map_diffuse"),
				GL.GetUniformLocation(ShaderProgram_ID, "map_gloss"),
				GL.GetUniformLocation(ShaderProgram_ID, "map_ao"),
				GL.GetUniformLocation(ShaderProgram_ID, "map_normal"),
				GL.GetUniformLocation(ShaderProgram_ID, "map_height")
			};
		}
	}

	/// <summary> Lighting shader program, with extra uniform IDs only needed for lighting shaders. </summary>
	public class ShaderProgramLighting : ShaderProgram {
		public ShaderProgramLighting(string unifiedPath) : base(unifiedPath) { }

		public int UniformCameraPosition_ID { get; private set; } = -1;
		private int SSBOLightData_ID;

		protected override void SetUniforms() {
			UniformCameraPosition_ID = GL.GetUniformLocation(ShaderProgram_ID, "cameraPosition");
			SSBOLightData_ID = GL.GenBuffer();
		}

		public void SetLightSSBO(List<Light> scene) {
			float[] buffer = new float[(scene.Count() * 10) + 1];
			buffer[0] = scene.Count();

			for (int i = 1; i < buffer.Length; i += 10) {
				Light l = (Light)scene[i / 10];
				float[] d = {
					l.Position.X, l.Position.Y, l.Position.Z,
					l.Color.X, l.Color.Y, l.Color.Z,
					l.Direction.X, l.Direction.Y, l.Direction.Z, l.Strength
				};
				Array.Copy(d, 0, buffer, i, d.Length);
			}

			GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, SSBOLightData_ID);
			GL.BufferData(BufferTarget.ShaderStorageBuffer, buffer.Length * sizeof(float), buffer, BufferUsageHint.StreamDraw);
		}

		protected override void SetOneTimeUniforms() {
			GL.Uniform1(GL.GetUniformLocation(ShaderProgram_ID, "gPosition"), 0);
			GL.Uniform1(GL.GetUniformLocation(ShaderProgram_ID, "gNormal"), 1);
			GL.Uniform1(GL.GetUniformLocation(ShaderProgram_ID, "gAlbedoSpec"), 2);
		}
	}
}