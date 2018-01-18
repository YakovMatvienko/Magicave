﻿using GameOfAllTimes.Core;
using System.Collections.Generic;
using System.Linq;
using RLNET;
using RogueSharp;
using System.Text;
using RogueSharp.DiceNotation;
using GameOfAllTimes.Behaviours;
using GameOfAllTimes.Interfaces;

namespace GameOfAllTimes.Systems
{
    public class CommandSystem
    {
        public bool IsPlayerTurn { get; set; }
        public bool MovePlayer(Direction direction)
        {
            int x = Game.Player.X;
            int y = Game.Player.Y;
            switch (direction)
            {
                case Direction.Up:
                    {
                        y = Game.Player.Y - 1;
                        break;
                    }
                case Direction.Down:
                    {
                        y = Game.Player.Y + 1;
                        break;
                    }
                case Direction.Left:
                    {
                        x = Game.Player.X - 1;
                        break;
                    }
                case Direction.Right:
                    {
                        x = Game.Player.X + 1;
                        break;
                    }
                default:
                    {
                        return false;
                    }
            }
            if (Game.DungeonMap.SetActorPosition(Game.Player, x, y))
                return true;
            Monster monster = Game.DungeonMap.GetMonsterAt(x, y);
            if (monster != null)
            {
                Attack(Game.Player, monster);
                return true;
            }
            return false;
        }
        public void Attack(Actor attacker, Actor defender)
        {
            StringBuilder attackMessage = new StringBuilder();
            StringBuilder defenseMessage = new StringBuilder();
            int hits = ResolveAttack(attacker, defender, attackMessage);
            int blocks = ResolveDefense(attacker, hits, defender, defenseMessage, attackMessage);
            Game.MessageLog.Add(attackMessage.ToString());
            if (!string.IsNullOrWhiteSpace(defenseMessage.ToString()))
            {
                Game.MessageLog.Add(defenseMessage.ToString());
            }
            int damage = hits - blocks;
            ResolveDamage(defender, damage);
        }
        private static void ResolveDamage(Actor defender, int damage)
        {
            if (damage > 0)
            {
                defender.Health -= damage;
                Game.MessageLog.Add($" {defender.Name} was hit for {damage} damage");
                if (defender.Health <= 0)
                {
                    ResolveDeath(defender);
                }
            }
            else
            {
                Game.MessageLog.Add($"{defender.Name} blocks all damage.");
            }
        }
        private static void ResolveDeath(Actor defender)
        {
            if (defender is Player)
            {
                Game.MessageLog.Add($"{defender.Name} was killed. GAME OVER MAAAN GAME OVER!!!");
                
            }
            else if (defender is Monster)
            {
                Game.Player.Gold += defender.Gold;
                Game.DungeonMap.RemoveMonster((Monster)defender);
                Game.MessageLog.Add($"  {defender.Name} died and dropped {defender.Gold} gold");
            }
        }
        private static int ResolveAttack(Actor attacker, Actor defender, StringBuilder attackMessage)
        {
            int hits = 0;
            attackMessage.AppendFormat("{0} attacks {1} and rolls: ", attacker.Name, defender.Name);
            DiceExpression attackDice = new DiceExpression().Dice(attacker.Attack, 100);
            DiceResult attackResult = attackDice.Roll();
            foreach (TermResult termResult in attackResult.Results)
            {
                attackMessage.Append(termResult.Value + ", ");
                if (termResult.Value >= 100 - attacker.AttackChance)
                {
                    hits++;
                }
            }
            return hits;
        }
        private static int ResolveDefense(Actor attacker, int hits, Actor defender, StringBuilder defenseMessage, StringBuilder attackMessage)
        {
            int blocks = 0;
            if (hits > 0)
            {
                attackMessage.AppendFormat("scoring {0} hits", hits);
                defenseMessage.AppendFormat("{0} defends {1} and rolls: ", defender.Name, attacker.Name);
                DiceExpression defenseDice = new DiceExpression().Dice(defender.Defense, 100);
                DiceResult defenseResult = defenseDice.Roll();
                foreach (TermResult termResult in defenseResult.Results)
                {
                    defenseMessage.Append(termResult.Value + ", ");
                    if (termResult.Value >= 100 - defender.DefenseChance)
                    {
                        blocks++;
                    }
                }
                defenseMessage.AppendFormat("resulting in {0} blocks", blocks);
            }
            else
            {
                attackMessage.Append("and misses completely.");
            }
            return blocks;
        }

        public void EndPlayerTurn()
        {
            IsPlayerTurn = false;
        }
        public void ActivateMonsters()
        {
            IScheduleable scheduleable = Game.SchedulingSystem.Get();
            if (scheduleable is Player)
            {
                IsPlayerTurn = true;
                Game.SchedulingSystem.Add(Game.Player);
            }
            else
            {
                Monster monster = scheduleable as Monster;

                if (monster != null)
                {
                    monster.PerformAction(this);
                    Game.SchedulingSystem.Add(monster);
                }

                ActivateMonsters();
            }
        }

        public void MoveMonster(Monster monster, Cell cell)
        {
            if (monster.Size == 1)
            {
                if (!Game.DungeonMap.SetActorPosition(monster, cell.X, cell.Y))
                {
                    if (Game.Player.X == cell.X && Game.Player.Y == cell.Y)
                    {
                        Attack(monster, Game.Player);
                    }
                }
            }
            else
            {
                List<Cell> ExpectedArea = Game.DungeonMap.GetCellsInArea(cell.X + 1, cell.Y + 1, 1).ToList();
                List<Cell> Sub = ExpectedArea.Except(monster.AreaControlled).ToList();
                if (Sub.Any(tile => !Game.DungeonMap.SetActorPosition(monster, tile.X, tile.Y)))
                {
                    if (Sub.Any(tile => Game.Player.X == tile.X && Game.Player.Y == tile.Y))
                    {
                        Attack(monster, Game.Player);
                    }
                }
            }
        }
    }
}
