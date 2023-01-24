using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("MazeGen", "Default", "0.2.6")]
    [Description("Creates mazes of various sizes for your players to solve.")]
    class MazeGen : RustPlugin
    {
        /*
        Credits to Pant for creating this original plugin. I have simply worked on it, to add better stability throughout, and soon more features.
        TODO
        Finish implementation of loot and NPC's in maze.
        Custom gamemode (Maybe?)

        */
        private System.Random random;
        private const string gen = "mazegen.gen";
        private static MazeGen instance = null;

        private List<Block> Grid;

        private float BlockSize = 3f;
        private float FoundationSize = 1f;
        private float BuldingHeight = 1f;
        private Vector3 RealGoal;
        private bool MakeRoof;

        

        private void Init()
        {
            instance = this;
            permission.RegisterPermission(gen, this);
            LoadMessages();
        }
        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "You don't have permission to use this command.",
            }, this);
        }

        [ChatCommand("maze.delete")]
        void dmaze(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, gen))
            {
                PrintToChat(player, lang.GetMessage("No Permission", this, player.UserIDString));
                return;
            }

            if (permission.UserHasPermission(player.UserIDString, gen))
            {
                if (Grid != null)
                {
                    foreach (Block block in Grid)
                    {
                        block.Destroye();
                    }
                }
            }
            
        }

        [ChatCommand("maze")]
        void GenerateMaze(BasePlayer player, string command, string[] args)
        {
            MakeRoof = true;

            if (!permission.UserHasPermission(player.UserIDString, gen))
            {
                PrintToChat(player, lang.GetMessage("No Permission", this, player.UserIDString));
                return;
            }

            if (permission.UserHasPermission(player.UserIDString, gen))
            {
                long seed = long.Parse(DateTime.Now.ToString("yyyyMMddHHmmss"));
                random = new System.Random((int)(seed / 100));

                BasePlayer Player = player;
                Grid = new List<Block>();

                try
                {
                    FoundationSize = float.Parse(args[0]);
                    if(FoundationSize > 30)
                    {
                        SendReply(player, "Maze too big");
                        return;
                    }
                }
                catch (Exception e)
                {
                    SendReply(player, "Size is not a valid number");
                }

                try
                {
                    BuldingHeight = float.Parse(args[1]);
                }
                catch (Exception e)
                {
                    SendReply(player, "Height is not a valid number");
                }

                try
                {
                    if(args[2] == "false")
                    {
                        MakeRoof = false;
                    }
                }
                catch (Exception e)
                {
                    SendReply(player, "Building roof is set to true");
                }

                RealGoal = new Vector3(FoundationSize - 1, BuldingHeight - 1, FoundationSize - 1);

                BuildGrid(player);
            }
            
        }

        private void BuildGrid(BasePlayer player)
        {
            for (float x = 0; x < FoundationSize; x++)
            {
                for (float y = 0; y < FoundationSize; y++)
                {
                    for (int z = 0; z < BuldingHeight; z++)
                    {
                        Prefab prefab;

                        if (z > 0)
                        {
                            prefab = Prefab.Floor;
                        }
                        else
                        {
                            prefab = Prefab.Foundation;
                        }

                        Block block = new Block(new Vector3(x * BlockSize, z * BlockSize, y * BlockSize) + player.GetNetworkPosition(), new Vector3(x, z, y), new Quaternion(), FoundationSize, BuldingHeight, prefab, random.Next(0, int.Parse(FoundationSize.ToString())), MakeRoof);
                        Grid.Add(block);
                    }
                }
            }

            SpawnMaze();
        }

        private void GeneratePath()
        {
            foreach (Block block in Grid)
            {

            }
        }

        private void SpawnMaze()
        {
            while ((from block in Grid where !block.visited select block).ToList().Count > 0)
            {
                Block block = (from foundation in Grid where !foundation.visited select foundation).FirstOrDefault();
                FindNext(block, true, RealGoal);
            }

            foreach (Block block in Grid)
            {
                block.SpawnEntitys();
            }
        }

        private void FindNext(Block block, bool New, Vector3 Goal, int RougCounter = 0)
        {
            block.visited = true;

            List<Block> Neighbours = GetNeighbours(block);
            float BestNeighbour = -1;
            Block BestNeighbourBlock = null;

            foreach (Block neighbours in Neighbours)
            {
                float distance = ManhattanDistance(neighbours.BasePosition, Goal, block.BasePosition) + neighbours.Weight;
                if (distance < BestNeighbour || BestNeighbour == -1)
                {
                    BestNeighbour = distance;
                    BestNeighbourBlock = neighbours;
                }
            }

            if (New)
            {
                Block OldNeighbour = GetNeighbours(block, true).FirstOrDefault();
                if (OldNeighbour != null)
                {
                    OpenWall(block, OldNeighbour);
                }
            }

            if (random.Next(0, 6) == 5 && Goal == RealGoal)
            {
                RougCounter = 10;
                Goal = new Vector3(random.Next(0, int.Parse(FoundationSize.ToString())), random.Next(0, int.Parse(BuldingHeight.ToString())), random.Next(0, int.Parse(FoundationSize.ToString())));
            }

            if (Goal != RealGoal)
            {
                if (!block.IsTheEnd(Goal))
                {
                    if (BestNeighbourBlock != null)
                    {
                        OpenWall(block, BestNeighbourBlock);
                        RougCounter--;

                        FindNext(BestNeighbourBlock, false, Goal, RougCounter);
                    }
                }
            }
            else
            {
                if (!block.IsTheEnd(Goal))
                {
                    if (BestNeighbourBlock != null)
                    {
                        LastVisited.Add(block);
                        OpenWall(block, BestNeighbourBlock);
                        FindNext(BestNeighbourBlock, false, RealGoal);
                    }
                    else
                    {
                        Block lastItem = LastVisited.LastOrDefault();
                        if (lastItem != null)
                        {
                            Vector3 goal = Goal == RealGoal ? RealGoal : Goal;
                            LastVisited.Remove(lastItem);
                            FindNext(lastItem, false, goal);
                        }
                    }
                }
            }
        }

        List<Block> LastVisited = new List<Block>();

        private void OpenWall(Block from, Block to)
        {
            if (from.BasePosition == to.BasePosition + new Vector3(0, 0, +1))
            {
                to.Walls[0].DisplayBlock = false;
            }
            if (from.BasePosition == to.BasePosition + new Vector3(+1, 0, 0))
            {
                from.Walls[1].DisplayBlock = false;
            }
            if (from.BasePosition == to.BasePosition + new Vector3(0, 0, -1))
            {
                from.Walls[0].DisplayBlock = false;
            }
            if (from.BasePosition == to.BasePosition + new Vector3(-1, 0, 0))
            {
                to.Walls[1].DisplayBlock = false;
            }
            if (from.BasePosition == to.BasePosition + new Vector3(0, -1, 0))
            {
                to.DisplayBlock = false;
                to.AddStair();
            }
            if (from.BasePosition == to.BasePosition + new Vector3(0, 1, 0))
            {
                from.DisplayBlock = false;
                from.AddStair();
            }
        }

        private float ManhattanDistance(Vector3 ToPosition, Vector3 Goal, Vector3 FromPosition)
        {
            float xd = ToPosition.x - Goal.x;
            float zd = ToPosition.z - Goal.z;
            float yd = ToPosition.y - Goal.y;

            if (ToPosition.y - FromPosition.y != 0)
            {
                yd += ((FoundationSize * BuldingHeight));
            }

            return Math.Abs(xd) + Math.Abs(zd) + Math.Abs(yd);
        }

        private List<Block> GetNeighbours(Block block, bool IsVisited = false)
        {
            List<Block> Neighbours = new List<Block>();

            Block block1 = (from foundation in Grid where foundation.BasePosition == new Vector3(block.BasePosition.x + 1, block.BasePosition.y, block.BasePosition.z) && ((!foundation.visited && !IsVisited) || (IsVisited && foundation.visited)) select foundation).FirstOrDefault();
            if (block1 != null)
            {
                Neighbours.Add(block1);
            }
            Block block2 = (from foundation in Grid where foundation.BasePosition == new Vector3(block.BasePosition.x, block.BasePosition.y, block.BasePosition.z + 1) && ((!foundation.visited && !IsVisited) || (IsVisited && foundation.visited)) select foundation).FirstOrDefault();
            if (block2 != null)
            {
                Neighbours.Add(block2);
            }
            Block block3 = (from foundation in Grid where foundation.BasePosition == new Vector3(block.BasePosition.x - 1, block.BasePosition.y, block.BasePosition.z) && ((!foundation.visited && !IsVisited) || (IsVisited && foundation.visited)) select foundation).FirstOrDefault();
            if (block3 != null)
            {
                Neighbours.Add(block3);
            }
            Block block4 = (from foundation in Grid where foundation.BasePosition == new Vector3(block.BasePosition.x, block.BasePosition.y, block.BasePosition.z - 1) && ((!foundation.visited && !IsVisited) || (IsVisited && foundation.visited)) select foundation).FirstOrDefault();
            if (block4 != null)
            {
                Neighbours.Add(block4);
            }
            Block block5 = (from foundation in Grid where foundation.BasePosition == new Vector3(block.BasePosition.x, block.BasePosition.y + 1, block.BasePosition.z) && ((!foundation.visited && !IsVisited) || (IsVisited && foundation.visited)) select foundation).FirstOrDefault();
            if (block5 != null)
            {
                Neighbours.Add(block5);
            }
            Block block6 = (from foundation in Grid where foundation.BasePosition == new Vector3(block.BasePosition.x, block.BasePosition.y - 1, block.BasePosition.z) && ((!foundation.visited && !IsVisited) || (IsVisited && foundation.visited)) select foundation).FirstOrDefault();
            if (block6 != null)
            {
                Neighbours.Add(block6);
            }
            return Neighbours;
        }
    }

    public enum Prefab
    {
        Wall,
        Foundation,
        Floor,
        StairUshape,
        FloorFrame,
        LadderHatch
    }

    public class Block
    {
        public Vector3 Position { get; set; }
        public Vector3 BasePosition { get; set; }
        public Quaternion Rotation { get; set; }

        public BaseEntity Entity { get; set; }
        public int EntityId { get; set; }
        public List<Block> Walls { get; set; }
        public Block Roof { get; set; }
        public Block Stair { get; set; }
        public Block FloorFrame { get; set; }
        public Block LadderHatch { get; set; }
        public int Weight { get; set; }
        public float foundationSize { get; set; }
        public float buildingHeight { get; set; }
        public bool DisplayBlock { get; set; }
        public bool visited { get; set; }

        public Block(Vector3 posion, Vector3 basePosition, Quaternion rotation, float FoundationSize, float BuildingHeight, Prefab prefab, int randomNumber, bool MakeRoof = false)
        {
            visited = false;
            Walls = new List<Block>();
            DisplayBlock = true;

            Position = posion;
            BasePosition = basePosition;
            Rotation = rotation;

            foundationSize = FoundationSize;
            buildingHeight = BuildingHeight;

            if ((prefab == Prefab.Foundation || prefab == Prefab.Floor) && basePosition.y + 1 == BuildingHeight && MakeRoof)
            {
                Roof = new Block(new Vector3(0f, 3f, 0f) + Position, BasePosition + new Vector3(0f, 1f, 0f), rotation, foundationSize, buildingHeight, Prefab.Floor, 0);
            }

            Weight = randomNumber;

            CreateEntity(prefab);

            if (prefab == Prefab.Foundation || (prefab == Prefab.Floor && BasePosition.y < BuildingHeight))
            {
                AddWalls();
            }
        }

        public bool IsTheEnd(Vector3 Goal)
        {
            if (BasePosition.x == Goal.x && BasePosition.z == Goal.z && BasePosition.y == Goal.y)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Destroye()
        {
            if (DisplayBlock)
            {
                //BuildingBlock block = Entity.GetComponent<BuildingBlock>();
                Entity.Kill();
            }
            if (Stair != null)
            {
                Stair.Destroye();
            }
            if (FloorFrame != null)
            {
                FloorFrame.Destroye();
            }
            if (LadderHatch != null)
            {
                LadderHatch.Destroye();
            }
            if (Roof != null)
            {
                Roof.Destroye();
            }
            foreach (Block wall in Walls)
            {
                wall.Destroye();
            }
        }

        public void SpawnEntitys()
        {
            if (DisplayBlock)
            {
                BuildingBlock block = Entity.GetComponent<BuildingBlock>();
                if (block != null)
                {

                }
                Entity.Spawn();
                if (block != null)
                {
                    block.SetGrade(BuildingGrade.Enum.TopTier);
                    block.SetHealthToMax();
                    block.cachedStability = 100;
                }
            }
            if (Stair != null)
            {
                Stair.SpawnEntitys();
            }
            if (FloorFrame != null)
            {
                FloorFrame.SpawnEntitys();
            }
            if (LadderHatch != null)
            {
                LadderHatch.SpawnEntitys();
            }
            if (Roof != null)
            {
                Roof.SpawnEntitys();
            }
            foreach (Block wall in Walls)
            {
                wall.SpawnEntitys();
            }
        }

        public void AddStair()
        {
            FloorFrame = new Block(Position, BasePosition, Rotation, foundationSize, buildingHeight, Prefab.FloorFrame, 0);
            LadderHatch = new Block(Position - new Vector3(0f, 0.1f, 0f), BasePosition, Rotation, foundationSize, buildingHeight, Prefab.LadderHatch, 0);
        }

        private void AddWalls()
        {
            Block wall1 = new Block(new Vector3(0f, 0f, 1.5f) + Position, BasePosition, new Quaternion(0f, -0.7071068f, 0f, 0.7071068f), foundationSize, buildingHeight, Prefab.Wall, 0);
            Walls.Add(wall1);

            Block wall2 = new Block(new Vector3(-1.5f, 0f, 0f) + Position, BasePosition, new Quaternion(0f, 1f, 0f, 0f), foundationSize, buildingHeight, Prefab.Wall, 0);
            Walls.Add(wall2);

            if (BasePosition.z == 0)
            {
                Block wall3 = new Block(new Vector3(0f, 0f, -1.5f) + Position, BasePosition, new Quaternion(0f, -0.7071068f, 0f, 0.7071068f), foundationSize, buildingHeight, Prefab.Wall, 0);
                Walls.Add(wall3);
            }

            if (BasePosition.x + 1 == foundationSize)
            {
                Block wall4 = new Block(new Vector3(1.5f, 0f, 0f) + Position, BasePosition, new Quaternion(0f, 1f, 0f, 0f), foundationSize, buildingHeight, Prefab.Wall, 0);
                Walls.Add(wall4);
            }
        }

        private void CreateEntity(Prefab prefab)
        {
            BaseEntity entity = null;
            switch (prefab)
            {
                case Prefab.Wall:
                    entity = GameManager.server.CreateEntity("assets/prefabs/building core/wall/wall.prefab", Position, Rotation, true);
                    break;
                case Prefab.Floor:
                    entity = GameManager.server.CreateEntity("assets/prefabs/building core/floor/floor.prefab", Position, Rotation, true);
                    break;
                case Prefab.Foundation:
                    entity = GameManager.server.CreateEntity("assets/prefabs/building core/foundation/foundation.prefab", Position, Rotation, true);
                    break;
                case Prefab.StairUshape:
                    entity = GameManager.server.CreateEntity("assets/prefabs/building/floor.ladder/floor.ladder.hatch.prefab", Position, Rotation, true);
                    break;
                case Prefab.LadderHatch:
                    entity = GameManager.server.CreateEntity("assets/prefabs/building/floor.ladder.hatch/floor.ladder.hatch.prefab", Position, Rotation, true);
                    break;
                case Prefab.FloorFrame:
                    entity = GameManager.server.CreateEntity("assets/prefabs/building core/floor.frame/floor.frame.prefab", Position, Rotation, true);
                    break;
            }
            AddEntity(entity);
        }

        private void AddEntity(BaseEntity entity)
        {
            if (entity != null)
            {
                Entity = entity;
                EntityId = entity.GetInstanceID();
            }
        }
    }
}