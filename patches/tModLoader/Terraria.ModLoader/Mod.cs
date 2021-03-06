using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.Liquid;
using Terraria.ID;
using Terraria.ModLoader.Exceptions;
using Terraria.ModLoader.IO;
using Terraria.Utilities;
using Terraria.Audio;

namespace Terraria.ModLoader
{
	public abstract partial class Mod
	{
		public TmodFile File { get; internal set; }
		public Assembly Code { get; internal set; }

		public virtual string Name => File.name;
		public virtual Version tModLoaderVersion => File.tModLoaderVersion;
		public virtual Version Version => File.version;

		public ModProperties Properties { get; protected set; }
		public ModSide Side { get; internal set; }
		public string DisplayName { get; internal set; }

		internal short netID = -1;

		internal readonly IDictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
		internal readonly IDictionary<string, SoundEffect> sounds = new Dictionary<string, SoundEffect>();
		internal readonly IDictionary<string, SpriteFont> fonts = new Dictionary<string, SpriteFont>();
		internal readonly IList<ModRecipe> recipes = new List<ModRecipe>();
		internal readonly IDictionary<string, ModItem> items = new Dictionary<string, ModItem>();
		internal readonly IDictionary<string, GlobalItem> globalItems = new Dictionary<string, GlobalItem>();
		internal readonly IDictionary<string, EquipTexture> equipTextures = new Dictionary<string, EquipTexture>();
		internal readonly IDictionary<string, ModDust> dusts = new Dictionary<string, ModDust>();
		internal readonly IDictionary<string, ModTile> tiles = new Dictionary<string, ModTile>();
		internal readonly IDictionary<string, GlobalTile> globalTiles = new Dictionary<string, GlobalTile>();
		internal readonly IDictionary<string, ModTileEntity> tileEntities = new Dictionary<string, ModTileEntity>();
		internal readonly IDictionary<string, ModWall> walls = new Dictionary<string, ModWall>();
		internal readonly IDictionary<string, GlobalWall> globalWalls = new Dictionary<string, GlobalWall>();
		internal readonly IDictionary<string, ModProjectile> projectiles = new Dictionary<string, ModProjectile>();
		internal readonly IDictionary<string, GlobalProjectile> globalProjectiles = new Dictionary<string, GlobalProjectile>();
		internal readonly IDictionary<string, ModNPC> npcs = new Dictionary<string, ModNPC>();
		internal readonly IDictionary<string, GlobalNPC> globalNPCs = new Dictionary<string, GlobalNPC>();
		internal readonly IDictionary<string, ModPlayer> players = new Dictionary<string, ModPlayer>();
		internal readonly IDictionary<string, ModMountData> mountDatas = new Dictionary<string, ModMountData>();
		internal readonly IDictionary<string, ModBuff> buffs = new Dictionary<string, ModBuff>();
		internal readonly IDictionary<string, GlobalBuff> globalBuffs = new Dictionary<string, GlobalBuff>();
		internal readonly IDictionary<string, ModWorld> worlds = new Dictionary<string, ModWorld>();
		internal readonly IDictionary<string, ModUgBgStyle> ugBgStyles = new Dictionary<string, ModUgBgStyle>();
		internal readonly IDictionary<string, ModSurfaceBgStyle> surfaceBgStyles = new Dictionary<string, ModSurfaceBgStyle>();
		internal readonly IDictionary<string, GlobalBgStyle> globalBgStyles = new Dictionary<string, GlobalBgStyle>();
		internal readonly IDictionary<string, ModWaterStyle> waterStyles = new Dictionary<string, ModWaterStyle>();
		internal readonly IDictionary<string, ModWaterfallStyle> waterfallStyles = new Dictionary<string, ModWaterfallStyle>();
		internal readonly IDictionary<string, GlobalRecipe> globalRecipes = new Dictionary<string, GlobalRecipe>();

		public virtual void Load()
		{
		}

		public virtual void PostSetupContent()
		{
		}

		public virtual void Unload()
		{
		}

		public virtual void AddRecipeGroups()
		{
		}

		public virtual void AddRecipes()
		{
		}

		internal void Autoload()
		{
			if (GetType().GetMethod("ChatInput", new Type[] { typeof(string) }) != null)
			{
				throw new OldHookException("Mod.ChatInput");
			}

			if (!Main.dedServ && File != null)
			{
				foreach (var file in File)
				{
					var path = file.Key;
					var data = file.Value;
					string extension = Path.GetExtension(path);
					switch (extension)
					{
						case ".png":
							string texturePath = Path.ChangeExtension(path, null);
							using (MemoryStream buffer = new MemoryStream(data))
							{
								textures[texturePath] = Texture2D.FromStream(Main.instance.GraphicsDevice, buffer);
								textures[texturePath].Name = Name + "/" + texturePath;
							}
							break;
						case ".wav":
							string soundPath = Path.ChangeExtension(path, null);
							using (MemoryStream buffer = new MemoryStream(data))
							{
								try
								{
									sounds[soundPath] = SoundEffect.FromStream(buffer);
								}
								catch
								{
									sounds[soundPath] = null;
								}
							}
							break;
						case ".mp3":
							string mp3Path = Path.ChangeExtension(path, null);
							string wavCacheFilename = this.Name + "_" + mp3Path.Replace('/', '_') + "_" + Version + ".wav";
							WAVCacheIO.DeleteIfOlder(File.path, wavCacheFilename);
							try
							{
								sounds[mp3Path] = WAVCacheIO.WAVCacheAvailable(wavCacheFilename)
									? SoundEffect.FromStream(WAVCacheIO.GetWavStream(wavCacheFilename))
									: WAVCacheIO.CacheMP3(wavCacheFilename, data);
							}
							catch
							{
								sounds[mp3Path] = null;
							}
							break;
						case ".xnb":
							string xnbPath = Path.ChangeExtension(path, null);
							if (xnbPath.StartsWith("Fonts/"))
							{
								string fontFilenameNoExtension = Name + "_" + xnbPath.Replace('/', '_') + "_" + Version;
								string fontFilename = fontFilenameNoExtension + ".xnb";
								FontCacheIO.DeleteIfOlder(File.path, fontFilename);
								if (!FontCacheIO.FontCacheAvailable(fontFilename))
								{
									FileUtilities.WriteAllBytes(FontCacheIO.FontCachePath + Path.DirectorySeparatorChar + fontFilename, data, false);
								}
								try
								{
									fonts[xnbPath] = Main.instance.Content.Load<SpriteFont>("Fonts" + Path.DirectorySeparatorChar + "ModFonts" + Path.DirectorySeparatorChar + fontFilenameNoExtension);
								}
								catch
								{
									fonts[xnbPath] = null;
								}
							}
							break;
					}
				}
			}

			if (Code == null)
				return;

			IList<Type> modGores = new List<Type>();
			IList<Type> modSounds = new List<Type>();
			foreach (Type type in Code.GetTypes().OrderBy(type=>type.FullName))
			{
				if (type.IsAbstract)
				{
					continue;
				}
				if (type.IsSubclassOf(typeof(ModItem)))
				{
					AutoloadItem(type);
				}
				else if (type.IsSubclassOf(typeof(GlobalItem)))
				{
					AutoloadGlobalItem(type);
				}
				else if (type.IsSubclassOf(typeof(ModDust)))
				{
					AutoloadDust(type);
				}
				else if (type.IsSubclassOf(typeof(ModTile)))
				{
					AutoloadTile(type);
				}
				else if (type.IsSubclassOf(typeof(GlobalTile)))
				{
					AutoloadGlobalTile(type);
				}
				else if (type.IsSubclassOf(typeof(ModTileEntity)))
				{
					AutoloadTileEntity(type);
				}
				else if (type.IsSubclassOf(typeof(ModWall)))
				{
					AutoloadWall(type);
				}
				else if (type.IsSubclassOf(typeof(GlobalWall)))
				{
					AutoloadGlobalWall(type);
				}
				else if (type.IsSubclassOf(typeof(ModProjectile)))
				{
					AutoloadProjectile(type);
				}
				else if (type.IsSubclassOf(typeof(GlobalProjectile)))
				{
					AutoloadGlobalProjectile(type);
				}
				else if (type.IsSubclassOf(typeof(ModNPC)))
				{
					AutoloadNPC(type);
				}
				else if (type.IsSubclassOf(typeof(GlobalNPC)))
				{
					AutoloadGlobalNPC(type);
				}
				else if (type.IsSubclassOf(typeof(ModPlayer)))
				{
					AutoloadPlayer(type);
				}
				else if (type.IsSubclassOf(typeof(ModBuff)))
				{
					AutoloadBuff(type);
				}
				else if (type.IsSubclassOf(typeof(GlobalBuff)))
				{
					AutoloadGlobalBuff(type);
				}
				else if (type.IsSubclassOf(typeof(ModMountData)))
				{
					AutoloadMountData(type);
				}
				else if (type.IsSubclassOf(typeof(ItemInfo)))
				{
					AutoloadItemInfo(type);
				}
				else if (type.IsSubclassOf(typeof(ProjectileInfo)))
				{
					AutoloadProjectileInfo(type);
				}
				else if (type.IsSubclassOf(typeof(NPCInfo)))
				{
					AutoloadNPCInfo(type);
				}
				else if (type.IsSubclassOf(typeof(ModGore)))
				{
					modGores.Add(type);
				}
				else if (type.IsSubclassOf(typeof(ModSound)))
				{
					modSounds.Add(type);
				}
				else if (type.IsSubclassOf(typeof(ModWorld)))
				{
					AutoloadModWorld(type);
				}
				else if (type.IsSubclassOf(typeof(ModUgBgStyle)))
				{
					AutoloadUgBgStyle(type);
				}
				else if (type.IsSubclassOf(typeof(ModSurfaceBgStyle)))
				{
					AutoloadSurfaceBgStyle(type);
				}
				else if (type.IsSubclassOf(typeof(GlobalBgStyle)))
				{
					AutoloadGlobalBgStyle(type);
				}
				else if (type.IsSubclassOf(typeof(ModWaterStyle)))
				{
					AutoloadWaterStyle(type);
				}
				else if (type.IsSubclassOf(typeof(ModWaterfallStyle)))
				{
					AutoloadWaterfallStyle(type);
				}
				else if (type.IsSubclassOf(typeof(GlobalRecipe)))
				{
					AutoloadGlobalRecipe(type);
				}
				else if (type.IsSubclassOf(typeof(ModCommand)))
				{
					AutoloadCommand(type);
				}
			}
			if (Properties.AutoloadGores)
			{
				AutoloadGores(modGores);
			}
			if (Properties.AutoloadSounds)
			{
				AutoloadSounds(modSounds);
			}
			if (Properties.AutoloadBackgrounds)
			{
				AutoloadBackgrounds();
			}
		}

		public void AddCommand(string name, ModCommand mc)
		{
			mc.Name = name;
			mc.Mod = this;
			
			CommandManager.Add(mc);
		}

		public void AddItem(string name, ModItem item, string texture)
		{
			Type colorClass = typeof(Microsoft.Xna.Framework.Color);
			Type floatClass = typeof(float);
			Type floatRefClass = floatClass.MakeByRefType();
			if (item.GetType().GetMethod("PreDrawInWorld", new Type[] {
				typeof(SpriteBatch), colorClass, colorClass, floatRefClass, floatRefClass,
				}) != null)
			{
				throw new OldHookException("ModItem.PreDrawInWorld");
			}
			if (item.GetType().GetMethod("PostDrawInWorld", new Type[] {
				typeof(SpriteBatch), colorClass, colorClass, floatClass, floatClass
				}) != null)
			{
				throw new OldHookException("ModItem.PostDrawInWorld");
			}
			int id = ItemLoader.ReserveItemID();
			item.item.name = name;
			item.item.ResetStats(id);
			item.item.modItem = item;
			if (items.ContainsKey(name))
			{
				throw new Exception("You tried to add 2 ModItems with the same name: " + name + ". Maybe 2 classes share a classname but in different namespaces while autoloading or you manually called AddItem with 2 items of the same name.");
			}
			items[name] = item;
			ItemLoader.items.Add(item);
			item.texture = texture;
			item.mod = this;
			if (item.IsQuestFish())
			{
				ItemLoader.questFish.Add(id);
			}
		}

		public ModItem GetItem(string name)
		{
			if (items.ContainsKey(name))
			{
				return items[name];
			}
			else
			{
				return null;
			}
		}

		public int ItemType(string name)
		{
			ModItem item = GetItem(name);
			if (item == null)
			{
				return 0;
			}
			return item.item.type;
		}

		public int ItemType<T>() where T : ModItem
		{
			return ItemType(typeof(T).Name);
		}

		public void AddGlobalItem(string name, GlobalItem globalItem)
		{
			Type colorClass = typeof(Microsoft.Xna.Framework.Color);
			Type floatClass = typeof(float);
			Type floatRefClass = floatClass.MakeByRefType();
			if (globalItem.GetType().GetMethod("PreDrawInWorld", new Type[] {
				typeof(Item), typeof(SpriteBatch), colorClass, colorClass, floatRefClass, floatRefClass
				}) != null)
			{
				throw new OldHookException("GlobalItem.PreDrawInWorld");
			}
			if (globalItem.GetType().GetMethod("PostDrawInWorld", new Type[] {
				typeof(Item), typeof(SpriteBatch), colorClass, colorClass, floatClass, floatClass
				}) != null)
			{
				throw new OldHookException("GlobalItem.PostDrawInWorld");
			}
			Type type = globalItem.GetType();
			globalItem.mod = this;
			globalItem.Name = name;
			this.globalItems[name] = globalItem;
			ItemLoader.globalItems.Add(globalItem);
		}

		public GlobalItem GetGlobalItem(string name)
		{
			if (this.globalItems.ContainsKey(name))
			{
				return this.globalItems[name];
			}
			else
			{
				return null;
			}
		}

		public void AddItemInfo(string name, ItemInfo info)
		{
			info.mod = this;
			info.Name = name;
			ItemLoader.infoIndexes[Name + ':' + name] = ItemLoader.infoList.Count;
			ItemLoader.infoList.Add(info);
		}

		public int AddEquipTexture(ModItem item, EquipType type, string name, string texture,
			string armTexture = "", string femaleTexture = "")
		{
			return AddEquipTexture(new EquipTexture(), item, type, name, texture, armTexture, femaleTexture);
		}

		public int AddEquipTexture(EquipTexture equipTexture, ModItem item, EquipType type, string name, string texture,
			string armTexture = "", string femaleTexture = "")
		{
			int slot = EquipLoader.ReserveEquipID(type);
			equipTexture.Texture = texture;
			equipTexture.mod = this;
			equipTexture.Name = name;
			equipTexture.Type = type;
			equipTexture.Slot = slot;
			equipTexture.item = item;
			EquipLoader.equipTextures[type][slot] = equipTexture;
			equipTextures[name] = equipTexture;
			ModLoader.GetTexture(texture);
			if (type == EquipType.Body)
			{
				EquipLoader.armTextures[slot] = armTexture;
				EquipLoader.femaleTextures[slot] = femaleTexture.Length > 0 ? femaleTexture : texture;
				ModLoader.GetTexture(armTexture);
				ModLoader.GetTexture(femaleTexture);
			}
			if (item != null && (type == EquipType.Head || type == EquipType.Body || type == EquipType.Legs))
			{
				EquipLoader.slotToId[type][slot] = item.item.type;
			}
			return slot;
		}

		public EquipTexture GetEquipTexture(string name)
		{
			if (equipTextures.ContainsKey(name))
			{
				return equipTextures[name];
			}
			else
			{
				return null;
			}
		}

		public int GetEquipSlot(string name)
		{
			EquipTexture texture = GetEquipTexture(name);
			if (texture == null)
			{
				return -1;
			}
			return texture.Slot;
		}

		public sbyte GetAccessorySlot(string name)
		{
			return (sbyte)GetEquipSlot(name);
		}

		public void AddFlameTexture(ModItem item, string texture)
		{
			ModLoader.GetTexture(texture);
			item.flameTexture = texture;
		}

		private void AutoloadItem(Type type)
		{
			ModItem item = (ModItem)Activator.CreateInstance(type);
			item.mod = this;
			string name = type.Name;
			string texture = (type.Namespace + "." + type.Name).Replace('.', '/');
			IList<EquipType> equips = new List<EquipType>();
			if (item.Autoload(ref name, ref texture, equips))
			{
				AddItem(name, item, texture);
				if (equips.Count > 0)
				{
					EquipLoader.idToSlot[item.item.type] = new Dictionary<EquipType, int>();
					foreach (EquipType equip in equips)
					{
						string equipTexture = texture + "_" + equip.ToString();
						string armTexture = texture + "_Arms";
						string femaleTexture = texture + "_FemaleBody";
						item.AutoloadEquip(equip, ref equipTexture, ref armTexture, ref femaleTexture);
						int slot = AddEquipTexture(item, equip, name, equipTexture, armTexture, femaleTexture);
						EquipLoader.idToSlot[item.item.type][equip] = slot;
					}
				}
				string flameTexture = texture + "_" + "Flame";
				item.AutoloadFlame(ref flameTexture);
				if (ModLoader.TextureExists(flameTexture))
				{
					AddFlameTexture(item, flameTexture);
				}
			}
		}

		private void AutoloadGlobalItem(Type type)
		{
			GlobalItem globalItem = (GlobalItem)Activator.CreateInstance(type);
			globalItem.mod = this;
			string name = type.Name;
			if (globalItem.Autoload(ref name))
			{
				AddGlobalItem(name, globalItem);
			}
		}

		private void AutoloadItemInfo(Type type)
		{
			ItemInfo itemInfo = (ItemInfo)Activator.CreateInstance(type);
			itemInfo.mod = this;
			string name = type.Name;
			if (itemInfo.Autoload(ref name))
			{
				AddItemInfo(name, itemInfo);
			}
		}

		public void AddDust(string name, ModDust dust, string texture = "")
		{
			int id = ModDust.ReserveDustID();
			ModDust.dusts.Add(dust);
			dust.Type = id;
			dust.Name = name;
			dust.Texture = texture.Length > 0 ? ModLoader.GetTexture(texture) : Main.dustTexture;
			dust.mod = this;
			dusts[name] = dust;
		}

		public ModDust GetDust(string name)
		{
			if (dusts.ContainsKey(name))
			{
				return dusts[name];
			}
			else
			{
				return null;
			}
		}

		public int DustType(string name)
		{
			ModDust dust = GetDust(name);
			if (dust == null)
			{
				return 0;
			}
			return dust.Type;
		}

		public int DustType<T>() where T : ModDust
		{
			return DustType(typeof(T).Name);
		}

		private void AutoloadDust(Type type)
		{
			ModDust dust = (ModDust)Activator.CreateInstance(type);
			dust.mod = this;
			string name = type.Name;
			string texture = (type.Namespace + "." + type.Name).Replace('.', '/');
			if (dust.Autoload(ref name, ref texture))
			{
				AddDust(name, dust, texture);
			}
		}

		public void AddTile(string name, ModTile tile, string texture)
		{
			int id = TileLoader.ReserveTileID();
			tile.Name = name;
			tile.Type = (ushort)id;
			tiles[name] = tile;
			TileLoader.tiles.Add(tile);
			tile.texture = texture;
			tile.mod = this;
		}

		public ModTile GetTile(string name)
		{
			if (tiles.ContainsKey(name))
			{
				return tiles[name];
			}
			else
			{
				return null;
			}
		}

		public int TileType(string name)
		{
			ModTile tile = GetTile(name);
			if (tile == null)
			{
				return 0;
			}
			return (int)tile.Type;
		}

		public int TileType<T>() where T : ModTile
		{
			return TileType(typeof(T).Name);
		}

		public void AddGlobalTile(string name, GlobalTile globalTile)
		{
			globalTile.mod = this;
			globalTile.Name = name;
			this.globalTiles[name] = globalTile;
			TileLoader.globalTiles.Add(globalTile);
		}

		public GlobalTile GetGlobalTile(string name)
		{
			if (this.globalTiles.ContainsKey(name))
			{
				return globalTiles[name];
			}
			else
			{
				return null;
			}
		}

		private void AutoloadTile(Type type)
		{
			ModTile tile = (ModTile)Activator.CreateInstance(type);
			tile.mod = this;
			string name = type.Name;
			string texture = (type.Namespace + "." + type.Name).Replace('.', '/');
			if (tile.Autoload(ref name, ref texture))
			{
				AddTile(name, tile, texture);
			}
		}

		private void AutoloadGlobalTile(Type type)
		{
			GlobalTile globalTile = (GlobalTile)Activator.CreateInstance(type);
			globalTile.mod = this;
			string name = type.Name;
			if (globalTile.Autoload(ref name))
			{
				AddGlobalTile(name, globalTile);
			}
		}

		public void AddTileEntity(string name, ModTileEntity entity)
		{
			int id = ModTileEntity.ReserveTileEntityID();
			entity.mod = this;
			entity.Name = name;
			entity.Type = id;
			entity.type = (byte)id;
			tileEntities[name] = entity;
			ModTileEntity.tileEntities.Add(entity);
		}

		public ModTileEntity GetTileEntity(string name)
		{
			if (tileEntities.ContainsKey(name))
			{
				return tileEntities[name];
			}
			else
			{
				return null;
			}
		}

		public int TileEntityType(string name)
		{
			ModTileEntity tileEntity = GetTileEntity(name);
			if (tileEntity == null)
			{
				return -1;
			}
			return tileEntity.Type;
		}

		private void AutoloadTileEntity(Type type)
		{
			ModTileEntity tileEntity = (ModTileEntity)Activator.CreateInstance(type);
			tileEntity.mod = this;
			string name = type.Name;
			if (tileEntity.Autoload(ref name))
			{
				AddTileEntity(name, tileEntity);
			}
		}

		public void AddWall(string name, ModWall wall, string texture)
		{
			int id = WallLoader.ReserveWallID();
			wall.Name = name;
			wall.Type = (ushort)id;
			walls[name] = wall;
			WallLoader.walls.Add(wall);
			wall.texture = texture;
			wall.mod = this;
		}

		public ModWall GetWall(string name)
		{
			if (walls.ContainsKey(name))
			{
				return walls[name];
			}
			else
			{
				return null;
			}
		}

		public int WallType(string name)
		{
			ModWall wall = GetWall(name);
			if (wall == null)
			{
				return 0;
			}
			return (int)wall.Type;
		}

		public int WallType<T>() where T : ModWall
		{
			return WallType(typeof(T).Name);
		}

		public void AddGlobalWall(string name, GlobalWall globalWall)
		{
			globalWall.mod = this;
			globalWall.Name = name;
			this.globalWalls[name] = globalWall;
			WallLoader.globalWalls.Add(globalWall);
		}

		public GlobalWall GetGlobalWall(string name)
		{
			if (this.globalWalls.ContainsKey(name))
			{
				return globalWalls[name];
			}
			else
			{
				return null;
			}
		}

		private void AutoloadWall(Type type)
		{
			ModWall wall = (ModWall)Activator.CreateInstance(type);
			wall.mod = this;
			string name = type.Name;
			string texture = (type.Namespace + "." + type.Name).Replace('.', '/');
			if (wall.Autoload(ref name, ref texture))
			{
				AddWall(name, wall, texture);
			}
		}

		private void AutoloadGlobalWall(Type type)
		{
			GlobalWall globalWall = (GlobalWall)Activator.CreateInstance(type);
			globalWall.mod = this;
			string name = type.Name;
			if (globalWall.Autoload(ref name))
			{
				AddGlobalWall(name, globalWall);
			}
		}

		public void AddProjectile(string name, ModProjectile projectile, string texture)
		{
			int id = ProjectileLoader.ReserveProjectileID();
			projectile.projectile.name = name;
			projectile.Name = name;
			projectile.projectile.type = id;
			projectiles[name] = projectile;
			ProjectileLoader.projectiles.Add(projectile);
			projectile.texture = texture;
			projectile.mod = this;
		}

		public ModProjectile GetProjectile(string name)
		{
			if (projectiles.ContainsKey(name))
			{
				return projectiles[name];
			}
			else
			{
				return null;
			}
		}

		public int ProjectileType(string name)
		{
			ModProjectile projectile = GetProjectile(name);
			if (projectile == null)
			{
				return 0;
			}
			return projectile.projectile.type;
		}
		
		public int ProjectileType<T>() where T : ModProjectile
		{
			return ProjectileType(typeof(T).Name);
		}

		public void AddGlobalProjectile(string name, GlobalProjectile globalProjectile)
		{
			globalProjectile.mod = this;
			globalProjectile.Name = name;
			this.globalProjectiles[name] = globalProjectile;
			ProjectileLoader.globalProjectiles.Add(globalProjectile);
		}

		public GlobalProjectile GetGlobalProjectile(string name)
		{
			if (this.globalProjectiles.ContainsKey(name))
			{
				return this.globalProjectiles[name];
			}
			else
			{
				return null;
			}
		}

		public void AddProjectileInfo(string name, ProjectileInfo info)
		{
			info.mod = this;
			info.Name = name;
			ProjectileLoader.infoIndexes[Name + ':' + name] = ProjectileLoader.infoList.Count;
			ProjectileLoader.infoList.Add(info);
		}

		private void AutoloadProjectile(Type type)
		{
			ModProjectile projectile = (ModProjectile)Activator.CreateInstance(type);
			projectile.mod = this;
			string name = type.Name;
			string texture = (type.Namespace + "." + type.Name).Replace('.', '/');
			if (projectile.Autoload(ref name, ref texture))
			{
				AddProjectile(name, projectile, texture);
			}
		}

		private void AutoloadGlobalProjectile(Type type)
		{
			GlobalProjectile globalProjectile = (GlobalProjectile)Activator.CreateInstance(type);
			globalProjectile.mod = this;
			string name = type.Name;
			if (globalProjectile.Autoload(ref name))
			{
				AddGlobalProjectile(name, globalProjectile);
			}
		}

		private void AutoloadProjectileInfo(Type type)
		{
			ProjectileInfo projectileInfo = (ProjectileInfo)Activator.CreateInstance(type);
			projectileInfo.mod = this;
			string name = type.Name;
			if (projectileInfo.Autoload(ref name))
			{
				AddProjectileInfo(name, projectileInfo);
			}
		}

		public void AddNPC(string name, ModNPC npc, string texture, string[] altTextures = null)
		{
			int id = NPCLoader.ReserveNPCID();
			npc.npc.name = name;
			npc.npc.type = id;
			npcs[name] = npc;
			NPCLoader.npcs.Add(npc);
			npc.texture = texture;
			npc.altTextures = altTextures;
			npc.mod = this;
		}

		public ModNPC GetNPC(string name)
		{
			if (npcs.ContainsKey(name))
			{
				return npcs[name];
			}
			else
			{
				return null;
			}
		}

		public int NPCType(string name)
		{
			ModNPC npc = GetNPC(name);
			if (npc == null)
			{
				return 0;
			}
			return npc.npc.type;
		}
		
		public int NPCType<T>() where T : ModNPC
		{
			return NPCType(typeof(T).Name);
		}

		public void AddGlobalNPC(string name, GlobalNPC globalNPC)
		{
			globalNPC.mod = this;
			globalNPC.Name = name;
			this.globalNPCs[name] = globalNPC;
			NPCLoader.globalNPCs.Add(globalNPC);
		}

		public GlobalNPC GetGlobalNPC(string name)
		{
			if (this.globalNPCs.ContainsKey(name))
			{
				return this.globalNPCs[name];
			}
			else
			{
				return null;
			}
		}

		public void AddNPCInfo(string name, NPCInfo info)
		{
			info.mod = this;
			info.Name = name;
			NPCLoader.infoIndexes[Name + ':' + name] = NPCLoader.infoList.Count;
			NPCLoader.infoList.Add(info);
		}

		public void AddNPCHeadTexture(int npcType, string texture)
		{
			int slot = NPCHeadLoader.ReserveHeadSlot();
			NPCHeadLoader.heads[texture] = slot;
			if (!Main.dedServ)
			{
				ModLoader.GetTexture(texture);
			}
			else if (Main.dedServ && !ModLoader.FileExists(texture + ".png"))
			{
				throw new MissingResourceException(texture);
			}
			NPCHeadLoader.npcToHead[npcType] = slot;
			NPCHeadLoader.headToNPC[slot] = npcType;
		}

		public void AddBossHeadTexture(string texture)
		{
			int slot = NPCHeadLoader.ReserveBossHeadSlot(texture);
			NPCHeadLoader.bossHeads[texture] = slot;
			ModLoader.GetTexture(texture);
		}

		private void AutoloadNPC(Type type)
		{
			ModNPC npc = (ModNPC)Activator.CreateInstance(type);
			npc.mod = this;
			string name = type.Name;
			string texture = (type.Namespace + "." + type.Name).Replace('.', '/');
			string defaultTexture = texture;
			string[] altTextures = new string[0];
			if (npc.Autoload(ref name, ref texture, ref altTextures))
			{
				AddNPC(name, npc, texture, altTextures);
				string headTexture = defaultTexture + "_Head";
				string bossHeadTexture = headTexture + "_Boss";
				npc.AutoloadHead(ref headTexture, ref bossHeadTexture);
				if (ModLoader.TextureExists(headTexture) || (Main.dedServ && ModLoader.FileExists(headTexture + ".png")))
				{
					AddNPCHeadTexture(npc.npc.type, headTexture);
				}
				if (ModLoader.TextureExists(bossHeadTexture))
				{
					AddBossHeadTexture(bossHeadTexture);
					NPCHeadLoader.npcToBossHead[npc.npc.type] = NPCHeadLoader.bossHeads[bossHeadTexture];
				}
			}
		}

		private void AutoloadGlobalNPC(Type type)
		{
			GlobalNPC globalNPC = (GlobalNPC)Activator.CreateInstance(type);
			globalNPC.mod = this;
			string name = type.Name;
			if (globalNPC.Autoload(ref name))
			{
				AddGlobalNPC(name, globalNPC);
			}
		}

		private void AutoloadNPCInfo(Type type)
		{
			NPCInfo npcInfo = (NPCInfo)Activator.CreateInstance(type);
			npcInfo.mod = this;
			string name = type.Name;
			if (npcInfo.Autoload(ref name))
			{
				AddNPCInfo(name, npcInfo);
			}
		}

		public void AddPlayer(string name, ModPlayer player)
		{
			Type itemClass = typeof(Item);
			Type intClass = typeof(int);
			if (player.GetType().GetMethod("CatchFish", new Type[] {
				itemClass, itemClass, intClass, intClass, intClass, intClass,
				intClass.MakeByRefType(), typeof(bool).MakeByRefType() }) != null)
			{
				throw new OldHookException("ModPlayer.CatchFish");
			}
			player.Name = name;
			players[name] = player;
			player.mod = this;
			PlayerHooks.Add(player);
		}

		private void AutoloadPlayer(Type type)
		{
			ModPlayer player = (ModPlayer)Activator.CreateInstance(type);
			player.mod = this;
			string name = type.Name;
			if (player.Autoload(ref name))
			{
				AddPlayer(name, player);
			}
		}

		public void AddBuff(string name, ModBuff buff, string texture)
		{
			int id = BuffLoader.ReserveBuffID();
			buff.Name = name;
			buff.Type = id;
			buffs[name] = buff;
			BuffLoader.buffs.Add(buff);
			buff.texture = texture;
			buff.mod = this;
		}

		public ModBuff GetBuff(string name)
		{
			if (buffs.ContainsKey(name))
			{
				return buffs[name];
			}
			else
			{
				return null;
			}
		}

		public int BuffType(string name)
		{
			ModBuff buff = GetBuff(name);
			if (buff == null)
			{
				return 0;
			}
			return buff.Type;
		}

		public int BuffType<T>() where T : ModBuff
		{
			return BuffType(typeof(T).Name);
		}

		public void AddGlobalBuff(string name, GlobalBuff globalBuff)
		{
			globalBuff.mod = this;
			globalBuff.Name = name;
			this.globalBuffs[name] = globalBuff;
			BuffLoader.globalBuffs.Add(globalBuff);
		}

		public GlobalBuff GetGlobalBuff(string name)
		{
			if (this.globalBuffs.ContainsKey(name))
			{
				return globalBuffs[name];
			}
			else
			{
				return null;
			}
		}

		private void AutoloadBuff(Type type)
		{
			ModBuff buff = (ModBuff)Activator.CreateInstance(type);
			buff.mod = this;
			string name = type.Name;
			string texture = (type.Namespace + "." + type.Name).Replace('.', '/');
			if (buff.Autoload(ref name, ref texture))
			{
				AddBuff(name, buff, texture);
			}
		}

		private void AutoloadGlobalBuff(Type type)
		{
			GlobalBuff globalBuff = (GlobalBuff)Activator.CreateInstance(type);
			globalBuff.mod = this;
			string name = type.Name;
			if (globalBuff.Autoload(ref name))
			{
				AddGlobalBuff(name, globalBuff);
			}
		}

		private void AutoloadMountData(Type type)
		{
			ModMountData mount = (ModMountData)Activator.CreateInstance(type);
			mount.mod = this;
			string name = type.Name;
			string texture = (type.Namespace + "." + type.Name).Replace('.', '/');
			IDictionary<MountTextureType, string> extraTextures = new Dictionary<MountTextureType, string>();
			foreach (MountTextureType textureType in Enum.GetValues(typeof(MountTextureType)))
			{
				extraTextures[textureType] = texture + "_" + textureType.ToString();
			}
			if (mount.Autoload(ref name, ref texture, extraTextures))
			{
				AddMount(name, mount, texture, extraTextures);
			}
		}

		public void AddMount(string name, ModMountData mount, string texture,
			IDictionary<MountTextureType, string> extraTextures = null)
		{
			int id;
			if (Mount.mounts == null || Mount.mounts.Length == MountID.Count)
			{
				Mount.Initialize();
			}
			id = MountLoader.ReserveMountID();
			mount.Name = name;
			mount.Type = id;
			mountDatas[name] = mount;
			MountLoader.mountDatas[id] = mount;
			mount.texture = texture;
			mount.mod = this;
			if (extraTextures != null)
			{
				foreach (MountTextureType textureType in Enum.GetValues(typeof(MountTextureType)))
				{
					if (extraTextures.ContainsKey(textureType) && ModLoader.TextureExists(extraTextures[textureType]))
					{
						Texture2D extraTexture = ModLoader.GetTexture(extraTextures[textureType]);
						switch (textureType)
						{
							case MountTextureType.Back:
								mount.mountData.backTexture = extraTexture;
								break;
							case MountTextureType.BackGlow:
								mount.mountData.backTextureGlow = extraTexture;
								break;
							case MountTextureType.BackExtra:
								mount.mountData.backTextureExtra = extraTexture;
								break;
							case MountTextureType.BackExtraGlow:
								mount.mountData.backTextureExtraGlow = extraTexture;
								break;
							case MountTextureType.Front:
								mount.mountData.frontTexture = extraTexture;
								break;
							case MountTextureType.FrontGlow:
								mount.mountData.frontTextureGlow = extraTexture;
								break;
							case MountTextureType.FrontExtra:
								mount.mountData.frontTextureExtra = extraTexture;
								break;
							case MountTextureType.FrontExtraGlow:
								mount.mountData.frontTextureExtraGlow = extraTexture;
								break;
						}
					}
				}
			}
		}

		public ModMountData GetMount(string name)
		{
			if (mountDatas.ContainsKey(name))
			{
				return mountDatas[name];
			}
			else
			{
				return null;
			}
		}

		public int MountType(string name)
		{
			ModMountData mountData = GetMount(name);
			if (mountData == null)
			{
				return 0;
			}
			return mountData.Type;
		}

		public int MountType<T>() where T : ModMountData
		{
			return MountType(typeof(T).Name);
		}

		public void AddModWorld(string name, ModWorld modWorld)
		{
			modWorld.Name = name;
			worlds[name] = modWorld;
			modWorld.mod = this;
			WorldHooks.Add(modWorld);
		}

		private void AutoloadModWorld(Type type)
		{
			ModWorld modWorld = (ModWorld)Activator.CreateInstance(type);
			modWorld.mod = this;
			string name = type.Name;
			if (modWorld.Autoload(ref name))
			{
				AddModWorld(name, modWorld);
			}
		}

		public ModWorld GetModWorld(string name)
		{
			if (worlds.ContainsKey(name))
			{
				return worlds[name];
			}
			else
			{
				return null;
			}
		}
		
		public T GetModWorld<T>() where T : ModWorld
		{
			return (T)GetModWorld(typeof(T).Name);
		}

		public void AddUgBgStyle(string name, ModUgBgStyle ugBgStyle)
		{
			int slot = UgBgStyleLoader.ReserveBackgroundSlot();
			ugBgStyle.mod = this;
			ugBgStyle.Name = name;
			ugBgStyle.Slot = slot;
			ugBgStyles[name] = ugBgStyle;
			UgBgStyleLoader.ugBgStyles.Add(ugBgStyle);
		}

		public ModUgBgStyle GetUgBgStyle(string name)
		{
			if (ugBgStyles.ContainsKey(name))
			{
				return ugBgStyles[name];
			}
			else
			{
				return null;
			}
		}

		private void AutoloadUgBgStyle(Type type)
		{
			ModUgBgStyle ugBgStyle = (ModUgBgStyle)Activator.CreateInstance(type);
			ugBgStyle.mod = this;
			string name = type.Name;
			if (ugBgStyle.Autoload(ref name))
			{
				AddUgBgStyle(name, ugBgStyle);
			}
		}

		public void AddSurfaceBgStyle(string name, ModSurfaceBgStyle surfaceBgStyle)
		{
			int slot = SurfaceBgStyleLoader.ReserveBackgroundSlot();
			surfaceBgStyle.mod = this;
			surfaceBgStyle.Name = name;
			surfaceBgStyle.Slot = slot;
			surfaceBgStyles[name] = surfaceBgStyle;
			SurfaceBgStyleLoader.surfaceBgStyles.Add(surfaceBgStyle);
		}

		public ModSurfaceBgStyle GetSurfaceBgStyle(string name)
		{
			if (surfaceBgStyles.ContainsKey(name))
			{
				return surfaceBgStyles[name];
			}
			else
			{
				return null;
			}
		}

		public int GetSurfaceBgStyleSlot(string name)
		{
			ModSurfaceBgStyle style = GetSurfaceBgStyle(name);
			return style == null ? -1 : style.Slot;
		}

		private void AutoloadSurfaceBgStyle(Type type)
		{
			ModSurfaceBgStyle surfaceBgStyle = (ModSurfaceBgStyle)Activator.CreateInstance(type);
			surfaceBgStyle.mod = this;
			string name = type.Name;
			if (surfaceBgStyle.Autoload(ref name))
			{
				AddSurfaceBgStyle(name, surfaceBgStyle);
			}
		}

		public void AddGlobalBgStyle(string name, GlobalBgStyle globalBgStyle)
		{
			globalBgStyle.mod = this;
			globalBgStyle.Name = name;
			globalBgStyles[name] = globalBgStyle;
			GlobalBgStyleLoader.globalBgStyles.Add(globalBgStyle);
		}

		public GlobalBgStyle GetGlobalBgStyle(string name)
		{
			if (globalBgStyles.ContainsKey(name))
			{
				return globalBgStyles[name];
			}
			else
			{
				return null;
			}
		}

		private void AutoloadGlobalBgStyle(Type type)
		{
			GlobalBgStyle globalBgStyle = (GlobalBgStyle)Activator.CreateInstance(type);
			globalBgStyle.mod = this;
			string name = type.Name;
			if (globalBgStyle.Autoload(ref name))
			{
				AddGlobalBgStyle(name, globalBgStyle);
			}
		}

		public void AddWaterStyle(string name, ModWaterStyle waterStyle, string texture, string blockTexture)
		{
			int style = WaterStyleLoader.ReserveStyle();
			waterStyle.mod = this;
			waterStyle.Name = name;
			waterStyle.Type = style;
			waterStyle.texture = texture;
			waterStyle.blockTexture = blockTexture;
			waterStyles[name] = waterStyle;
			WaterStyleLoader.waterStyles.Add(waterStyle);
		}

		public ModWaterStyle GetWaterStyle(string name)
		{
			if (waterStyles.ContainsKey(name))
			{
				return waterStyles[name];
			}
			else
			{
				return null;
			}
		}

		private void AutoloadWaterStyle(Type type)
		{
			ModWaterStyle waterStyle = (ModWaterStyle)Activator.CreateInstance(type);
			waterStyle.mod = this;
			string name = type.Name;
			string texture = (type.Namespace + "." + type.Name).Replace('.', '/');
			string blockTexture = texture + "_Block";
			if (waterStyle.Autoload(ref name, ref texture, ref blockTexture))
			{
				AddWaterStyle(name, waterStyle, texture, blockTexture);
			}
		}

		public void AddWaterfallStyle(string name, ModWaterfallStyle waterfallStyle, string texture)
		{
			int slot = WaterfallStyleLoader.ReserveStyle();
			waterfallStyle.mod = this;
			waterfallStyle.Name = name;
			waterfallStyle.Type = slot;
			waterfallStyle.texture = texture;
			waterfallStyles[name] = waterfallStyle;
			WaterfallStyleLoader.waterfallStyles.Add(waterfallStyle);
		}

		public ModWaterfallStyle GetWaterfallStyle(string name)
		{
			if (waterfallStyles.ContainsKey(name))
			{
				return waterfallStyles[name];
			}
			else
			{
				return null;
			}
		}

		public int GetWaterfallStyleSlot(string name)
		{
			ModWaterfallStyle style = GetWaterfallStyle(name);
			return style == null ? -1 : style.Type;
		}

		private void AutoloadWaterfallStyle(Type type)
		{
			ModWaterfallStyle waterfallStyle = (ModWaterfallStyle)Activator.CreateInstance(type);
			waterfallStyle.mod = this;
			string name = type.Name;
			string texture = (type.Namespace + "." + type.Name).Replace('.', '/');
			if (waterfallStyle.Autoload(ref name, ref texture))
			{
				AddWaterfallStyle(name, waterfallStyle, texture);
			}
		}

		public void AddGore(string texture, ModGore modGore = null)
		{
			int id = ModGore.ReserveGoreID();
			ModGore.gores[texture] = id;
			if (modGore != null)
			{
				ModGore.modGores[id] = modGore;
			}
		}

		public int GetGoreSlot(string name)
		{
			return ModGore.GetGoreSlot(Name + '/' + name);
		}

		public int GetGoreSlot<T>() where T : ModGore
		{
			return GetGoreSlot(typeof(T).Name);
		}

		private void AutoloadGores(IList<Type> modGores)
		{
			var modGoreNames = modGores.ToDictionary(t => t.Namespace + "." + t.Name);
			foreach (var texture in textures.Keys.Where(t => t.StartsWith("Gores/")))
			{
				ModGore modGore = null;
				Type t;
				if (modGoreNames.TryGetValue(Name + "." + texture.Replace('/', '.'), out t))
					modGore = (ModGore)Activator.CreateInstance(t);

				AddGore(Name + '/' + texture, modGore);
			}
		}

		public void AddSound(SoundType type, string soundPath, ModSound modSound = null)
		{
			int id = SoundLoader.ReserveSoundID(type);
			SoundLoader.sounds[type][soundPath] = id;
			if (modSound != null)
			{
				SoundLoader.modSounds[type][id] = modSound;
				modSound.sound = ModLoader.GetSound(soundPath);
			}
		}

		public int GetSoundSlot(SoundType type, string name)
		{
			return SoundLoader.GetSoundSlot(type, Name + '/' + name);
		}

		public LegacySoundStyle GetLegacySoundSlot(SoundType type, string name)
		{
			return SoundLoader.GetLegacySoundSlot(type, Name + '/' + name);
		}

		private void AutoloadSounds(IList<Type> modSounds)
		{
			var modSoundNames = modSounds.ToDictionary(t => t.Namespace + "." + t.Name);
			foreach (var sound in sounds.Keys.Where(t => t.StartsWith("Sounds/")))
			{
				string substring = sound.Substring("Sounds/".Length);
				SoundType soundType = SoundType.Custom;
				if (substring.StartsWith("Item/"))
				{
					soundType = SoundType.Item;
				}
				else if (substring.StartsWith("NPCHit/"))
				{
					soundType = SoundType.NPCHit;
				}
				else if (substring.StartsWith("NPCKilled/"))
				{
					soundType = SoundType.NPCKilled;
				}
				else if (substring.StartsWith("Music/"))
				{
					soundType = SoundType.Music;
				}
				ModSound modSound = null;
				Type t;
				if (modSoundNames.TryGetValue((Name + '/' + sound).Replace('/', '.'), out t))
					modSound = (ModSound)Activator.CreateInstance(t);

				AddSound(soundType, Name + '/' + sound, modSound);
			}
		}

		public void AddBackgroundTexture(string texture)
		{
			int slot = BackgroundTextureLoader.ReserveBackgroundSlot();
			BackgroundTextureLoader.backgrounds[texture] = slot;
			ModLoader.GetTexture(texture);
		}

		public int GetBackgroundSlot(string name)
		{
			return BackgroundTextureLoader.GetBackgroundSlot(Name + '/' + name);
		}

		private void AutoloadBackgrounds()
		{
			foreach (string texture in textures.Keys.Where(t => t.StartsWith("Backgrounds/")))
			{
				AddBackgroundTexture(Name + '/' + texture);
			}
		}

		public void AddGlobalRecipe(string name, GlobalRecipe globalRecipe)
		{
			globalRecipe.Name = name;
			globalRecipes[name] = globalRecipe;
			globalRecipe.mod = this;
			RecipeHooks.Add(globalRecipe);
		}

		private void AutoloadGlobalRecipe(Type type)
		{
			GlobalRecipe globalRecipe = (GlobalRecipe)Activator.CreateInstance(type);
			globalRecipe.mod = this;
			string name = type.Name;
			if (globalRecipe.Autoload(ref name))
			{
				AddGlobalRecipe(name, globalRecipe);
			}
		}

		private void AutoloadCommand(Type type)
		{
			var mc = (ModCommand) Activator.CreateInstance(type);
			mc.Mod = this;
			var name = type.Name;
			if (mc.Autoload(ref name))
				AddCommand(name, mc);
		}

		public GlobalRecipe GetGlobalRecipe(string name)
		{
			if (globalRecipes.ContainsKey(name))
			{
				return globalRecipes[name];
			}
			else
			{
				return null;
			}
		}

		public void AddMusicBox(int musicSlot, int itemType, int tileType, int tileFrameY = 0)
		{
			if (musicSlot < Main.maxMusic)
			{
				throw new ArgumentOutOfRangeException("Cannot assign music box to vanilla music ID " + musicSlot);
			}
			if (musicSlot >= SoundLoader.SoundCount(SoundType.Music))
			{
				throw new ArgumentOutOfRangeException("Music ID " + musicSlot + " does not exist");
			}
			if (itemType < ItemID.Count)
			{
				throw new ArgumentOutOfRangeException("Cannot assign music box to vanilla item ID " + itemType);
			}
			if (ItemLoader.GetItem(itemType) == null)
			{
				throw new ArgumentOutOfRangeException("Item ID " + itemType + " does not exist");
			}
			if (tileType < TileID.Count)
			{
				throw new ArgumentOutOfRangeException("Cannot assign music box to vanilla tile ID " + tileType);
			}
			if (TileLoader.GetTile(tileType) == null)
			{
				throw new ArgumentOutOfRangeException("Tile ID " + tileType + " does not exist");
			}
			if (SoundLoader.musicToItem.ContainsKey(musicSlot))
			{
				throw new ArgumentException("Music ID " + musicSlot + " has already been assigned a music box");
			}
			if (SoundLoader.itemToMusic.ContainsKey(itemType))
			{
				throw new ArgumentException("Item ID " + itemType + " has already been assigned a music");
			}
			if (!SoundLoader.tileToMusic.ContainsKey(tileType))
			{
				SoundLoader.tileToMusic[tileType] = new Dictionary<int, int>();
			}
			if (SoundLoader.tileToMusic[tileType].ContainsKey(tileFrameY))
			{
				string message = "Y-frame " + tileFrameY + " of tile type " + tileType + " has already been assigned a music";
				throw new ArgumentException(message);
			}
			if (tileFrameY % 36 != 0)
			{
				throw new ArgumentException("Y-frame must be divisible by 36");
			}
			SoundLoader.musicToItem[musicSlot] = itemType;
			SoundLoader.itemToMusic[itemType] = musicSlot;
			SoundLoader.tileToMusic[tileType][tileFrameY] = musicSlot;
		}

		public ModHotKey RegisterHotKey(string name, string defaultKey)
		{
			return ModLoader.RegisterHotKey(this, name, defaultKey);
		}

		internal void SetupContent()
		{
			foreach (ModItem item in items.Values)
			{
				Main.itemTexture[item.item.type] = ModLoader.GetTexture(item.texture);
				Main.itemName[item.item.type] = item.item.name;
				EquipLoader.SetSlot(item.item);
				ItemLoader.SetupItemInfo(item.item);
				item.SetDefaults();
				DrawAnimation animation = item.GetAnimation();
				if (animation != null)
				{
					Main.RegisterItemAnimation(item.item.type, animation);
					ItemLoader.animations.Add(item.item.type);
				}
				if (item.flameTexture.Length > 0)
				{
					Main.itemFlameTexture[item.item.type] = ModLoader.GetTexture(item.flameTexture);
				}
			}
			foreach (ModDust dust in dusts.Values)
			{
				dust.SetDefaults();
			}
			foreach (ModTile tile in tiles.Values)
			{
				Main.tileTexture[tile.Type] = ModLoader.GetTexture(tile.texture);
				TileLoader.SetDefaults(tile);
			}
			foreach (GlobalTile globalTile in globalTiles.Values)
			{
				globalTile.SetDefaults();
			}
			foreach (ModWall wall in walls.Values)
			{
				Main.wallTexture[wall.Type] = ModLoader.GetTexture(wall.texture);
				wall.SetDefaults();
			}
			foreach (GlobalWall globalWall in globalWalls.Values)
			{
				globalWall.SetDefaults();
			}
			foreach (ModProjectile projectile in projectiles.Values)
			{
				Main.projectileTexture[projectile.projectile.type] = ModLoader.GetTexture(projectile.texture);
				Main.projFrames[projectile.projectile.type] = 1;
				ProjectileLoader.SetupProjectileInfo(projectile.projectile);
				projectile.SetDefaults();
				if (projectile.projectile.hostile)
				{
					Main.projHostile[projectile.projectile.type] = true;
				}
				if (projectile.projectile.aiStyle == 7)
				{
					Main.projHook[projectile.projectile.type] = true;
				}
			}
			foreach (ModNPC npc in npcs.Values)
			{
				Main.npcTexture[npc.npc.type] = ModLoader.GetTexture(npc.texture);
				Main.npcName[npc.npc.type] = npc.npc.name;
				Main.npcNameEnglish[npc.npc.type] = npc.npc.name;
				NPCLoader.SetupNPCInfo(npc.npc);
				npc.SetDefaults();
				if(npc.banner !=0)
				{
					NPCLoader.bannerToItem[npc.banner] = npc.bannerItem;
				}
				if (npc.npc.lifeMax > 32767 || npc.npc.boss)
				{
					Main.npcLifeBytes[npc.npc.type] = 4;
				}
				else if (npc.npc.lifeMax > 127)
				{
					Main.npcLifeBytes[npc.npc.type] = 2;
				}
				else
				{
					Main.npcLifeBytes[npc.npc.type] = 1;
				}
				int altTextureCount = NPCID.Sets.ExtraTextureCount[npc.npc.type];
				Main.npcAltTextures[npc.npc.type] = new Texture2D[altTextureCount + 1];
				if (altTextureCount > 0)
				{
					Main.npcAltTextures[npc.npc.type][0] = Main.npcTexture[npc.npc.type];
				}
				for (int k = 1; k <= altTextureCount; k++)
				{
					Main.npcAltTextures[npc.npc.type][k] = ModLoader.GetTexture(npc.altTextures[k - 1]);
				}
			}
			foreach (ModMountData modMountData in mountDatas.Values)
			{
				var mountData = modMountData.mountData;
				mountData.modMountData = modMountData;
				MountLoader.SetupMount(mountData);
				Mount.mounts[modMountData.Type] = mountData;
			}
			foreach (ModBuff buff in buffs.Values)
			{
				Main.buffTexture[buff.Type] = ModLoader.GetTexture(buff.texture);
				Main.buffName[buff.Type] = buff.Name;
				buff.SetDefaults();
			}
			foreach (ModWaterStyle waterStyle in waterStyles.Values)
			{
				LiquidRenderer.Instance._liquidTextures[waterStyle.Type] = ModLoader.GetTexture(waterStyle.texture);
				Main.liquidTexture[waterStyle.Type] = ModLoader.GetTexture(waterStyle.blockTexture);
			}
			foreach (ModWaterfallStyle waterfallStyle in waterfallStyles.Values)
			{
				Main.instance.waterfallManager.waterfallTexture[waterfallStyle.Type]
					= ModLoader.GetTexture(waterfallStyle.texture);
			}
		}

		internal void UnloadContent()
		{
			Unload();
			recipes.Clear();
			items.Clear();
			globalItems.Clear();
			equipTextures.Clear();
			dusts.Clear();
			tiles.Clear();
			globalTiles.Clear();
			tileEntities.Clear();
			walls.Clear();
			globalWalls.Clear();
			projectiles.Clear();
			globalProjectiles.Clear();
			npcs.Clear();
			globalNPCs.Clear();
			buffs.Clear();
			globalBuffs.Clear();
			worlds.Clear();
			ugBgStyles.Clear();
			surfaceBgStyles.Clear();
			globalBgStyles.Clear();
			waterStyles.Clear();
			waterfallStyles.Clear();
			globalRecipes.Clear();
		}

		public byte[] GetFileBytes(string name)
		{
			return File?.GetFile(name);
		}

		public bool FileExists(string name)
		{
			return File != null && File.HasFile(name);
		}

		public Texture2D GetTexture(string name)
		{
			Texture2D t;
			if (!textures.TryGetValue(name, out t))
				throw new MissingResourceException(name);

			return t;
		}

		public bool TextureExists(string name)
		{
			return textures.ContainsKey(name);
		}

		public void AddTexture(string name, Texture2D texture)
		{
			if (TextureExists(name))
				throw new ModNameException("Texture already exist: " + name);

			textures[name] = texture;
		}

		public SoundEffect GetSound(string name)
		{
			SoundEffect sound;
			if (!sounds.TryGetValue(name, out sound))
				throw new MissingResourceException(name);

			return sound;
		}

		public bool SoundExists(string name)
		{
			return sounds.ContainsKey(name);
		}

		public SpriteFont GetFont(string name)
		{
			SpriteFont font;
			if (!fonts.TryGetValue(name, out font))
				throw new MissingResourceException(name);

			return font;
		}

		public bool FontExists(string name)
		{
			return fonts.ContainsKey(name);
		}

		/// <summary>
		/// For weak inter-mod communication.
		/// </summary>
		public virtual object Call(params object[] args)
		{
			return null;
		}

		public ModPacket GetPacket(int capacity = 256)
		{
			if (netID < 0)
				throw new Exception("Cannot get packet for " + Name + " because it does not exist on the other side");

			var p = new ModPacket(MessageID.ModPacket, capacity + 5);
			p.Write(netID);
			return p;
		}
	}
}
