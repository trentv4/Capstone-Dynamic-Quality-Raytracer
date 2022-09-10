using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace DominusCore {
	public struct Soldier {
		public int Health { get; private set; }
		public UnitType Type { get; private set; }
		public Drawable Drawable;
		public Vector3 Position;
		public float Rotation;

		public Soldier(UnitType t) {
			Health = t.MaxHealth;
			Type = t;
			Drawable = null;
			Position = Vector3.Zero;
			Rotation = 0;
		}
	}

	public class UnitType {
		public readonly string Name;
		public readonly int MaxHealth;
		public readonly string ModelFileLocation;

		public UnitType(string name, int health, string model) {
			this.Name = name;
			this.MaxHealth = health;
			this.ModelFileLocation = model;
		}

		public static UnitType GetTypeByID(string id, Gamepack c) {
			if (!c.UnitTypes.ContainsKey(id)) {
				Console.WriteLine($"Error: Unable to find type {id}");
			}
			return c.UnitTypes.GetValueOrDefault(id); ;
		}
	}
}
