using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using CodeHatch.Common;
using CodeHatch.Damaging;
using CodeHatch.Engine.Core.Cache;
using CodeHatch.Engine.Core.Commands;
using CodeHatch.Engine.Behaviours;
using CodeHatch.Engine.Entities.Definitions;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Engine.Modules.SocialSystem.Objects;
using CodeHatch.Engine.Networking;
using CodeHatch.Engine.Serialization;
using CodeHatch.Networking.Events;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Entities.Players;
using CodeHatch.Networking.Events.Gaming;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Thrones;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Thrones.Weapons.Events;
using CodeHatch.UserInterface.Dialogues;
using CodeHatch.UserInterface.General;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins{
    [Info("DuelsManager", "PierreA", "0.0.2")]
    public class DuelsManager : ReignOfKingsPlugin{
		
		#region Configuration Data
		private Collection<Duels> _ListeDuels = new Collection<Duels>();
		private float[] positionVide = new float[]{0f,0f,0f};
		#endregion
		
		#region classes
		private class Duels{
			public ulong joueurDefieur;
			public string joueurDefieurName;
			public ulong joueurReceveurDefi;
			public string joueurReceveurDefiName;
			public float[] positionDepart;
			public bool duelEnCours;
			public int timestampDebutDuel;
		}
		#endregion
		
		#region Config save/load	
		private void Loaded()
        {
            LoadDefaultMessages();
			setUpTimerDuels();
        }
		
		private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "IncorrectCmd", "Syntaxe incorrecte: /defier pseudoJoueur" },
				{ "UnknownPlayer", "Joueur inconnu ou offline" },
				{ "NoSelfDefy", "Vous ne pouvez pas vous défier vous-même..." },
				{ "MaxRange", "Votre adversaire doit se trouver à 15m de vous au maximum." },
				{ "AlreadyInDefyWith", "Vous êtes déjà en duel avec " },
				{ "AlreadyAsk", "Vous avez déjà demandé un duel à " },
				{ "DefyStarted", "Un défi à débuté entre " },
				{ "And", " et " },
				{ "DefyReceived", " vous a lancé un défi. Tapez /defier <pseudoJoueur> pour l'accepter" },
				{ "DefySentTo", "Défi envoyé à " },
				{ "WonADuel", " a gagné un duel contre " }
            }, this,"fr");
			
			lang.RegisterMessages(new Dictionary<string, string>
            {
                { "IncorrectCmd", "Incorrect command: /defy <playerName>" },
                { "UnknownPlayer", "Player offline or unknown" },
                { "NoSelfDefy", "You cannot defy yourself..." },
				{ "MaxRange", "Max defy range is 15m" },
				{ "AlreadyInDefyWith", "You are already in defy with " },
				{ "AlreadyAsk", "You already defy " },
				{ "DefyStarted", " duel started between " },
				{ "And", " and " },
				{ "DefyReceived", " sent you a defy. Tip /defy <playerNick> to accept it" },
				{ "DefySentTo", "Defy sent to " },
				{ "WonADuel", " won a duel against " }
            }, this,"en");
        }	
		#endregion
		
		#region Commands
		[ChatCommand("defier")]
		private void defier(Player defieur, string cmd, string[] args){
			defy(defieur,cmd, args);
		}
		
		[ChatCommand("defy")]
        private void defy(Player defieur, string cmd, string[] args){
			if(args.Length < 1){
				PrintToChat(defieur,GetMessage("IncorrectCmd",defieur.Id));
			}
			else{
				if(!isOnline(args[0])){
					PrintToChat(defieur,GetMessage("UnknownPlayer",defieur.Id));
				}
				else{
					//joueur défié existant
					Player receveurDefi = Server.GetPlayerByName(args[0]);
					if(receveurDefi.Id == defieur.Id){
						PrintToChat(defieur,GetMessage("NoSelfDefy",defieur.Id));
					}
					else{
						if(DistancePointAPointB(getPosition(defieur),getPosition(receveurDefi))> 15f){
							// cible <= 15 mêtres?
							PrintToChat(defieur,GetMessage("MaxRange",defieur.Id));					
						}
						else{
							//check si déjà un défi
							Duels leDuel = null;
							foreach(Duels unDuel in _ListeDuels){
								if(unDuel.joueurDefieur == defieur.Id && unDuel.joueurReceveurDefi == receveurDefi.Id ||unDuel.joueurDefieur == receveurDefi.Id && unDuel.joueurReceveurDefi == defieur.Id){
									leDuel = unDuel;
								}
							}
							if(leDuel != null){
								//deja un duel prévu ou en cours entre les 2 joueurs
								Player adversaire = Server.GetPlayerById(leDuel.joueurReceveurDefi);
								if(defieur.Id == leDuel.joueurReceveurDefi){
									adversaire = Server.GetPlayerById(leDuel.joueurDefieur);
								}
								if(leDuel.duelEnCours){
									//deja un duel en cours
									PrintToChat(defieur,GetMessage("MaxRange",defieur.Id)+adversaire.Name);
								}
								else{
									//deja un duel pas validé
									if(defieur.Id == leDuel.joueurDefieur){
										//relance défi
										PrintToChat(defieur,GetMessage("AlreadyAsk",defieur.Id)+adversaire.Name);
									}
									else{
										//acceptation duel
										PrintToChat(GetMessage("DefyStarted",defieur.Id)+"[0080FF]"+leDuel.joueurDefieurName+"[FFFFFF]"+GetMessage("And",defieur.Id)+"[0080FF]"+leDuel.joueurReceveurDefiName+"[FFFFFF] !");
										leDuel.duelEnCours = true;
									}
								}
							}
							else{
								//rien de prévu entre les 2 joueurs on crée le défi
								_ListeDuels.Add(
									new Duels{
										joueurDefieur = defieur.Id,
										joueurDefieurName = defieur.Name,
										joueurReceveurDefi = receveurDefi.Id,
										joueurReceveurDefiName = receveurDefi.Name,
										positionDepart = getPosition(defieur),
										duelEnCours = false,
										timestampDebutDuel = getTimestamp()
									}
								);
								PrintToChat(receveurDefi,defieur.Name+GetMessage("DefyReceived",receveurDefi.Id));
								PrintToChat(defieur,GetMessage("DefySentTo",defieur.Id)+receveurDefi.Name);
							}
						}
					}
				}
			}
		}
		
		#endregion
		
		#region Hooks
		private void OnEntityHealthChange(EntityDamageEvent e){
			
			Entity victimeDegats = e.Entity;
			Player joueurVictime = e.Entity.Owner;
			Entity sourceDegats = e.Damage.DamageSource;
			Player joueurAttaquant = e.Damage.DamageSource.Owner;
			
			if(victimeDegats.IsPlayer && sourceDegats.IsPlayer){
				foreach(Duels unDuel in _ListeDuels){
					if(unDuel.joueurDefieur == joueurVictime.Id || unDuel.joueurReceveurDefi == joueurVictime.Id){
						//si dans la liste des duels
						if(unDuel.joueurDefieur == joueurAttaquant.Id || unDuel.joueurReceveurDefi == joueurAttaquant.Id){
							//si un duel avant le tueur
							if(unDuel.duelEnCours){
								//si duel actif
								if(isDamageKilling(joueurVictime, e.Damage)){
									//si point de vie à 0 tête ou torse
									PrintToChat("[0080FF]"+joueurAttaquant.Name+"[FFFFFF]"+GetMessage("WonADuel",joueurAttaquant.Id)+"[0080FF]"+joueurVictime.Name+"[FFFFFF] !");
									joueurAttaquant.GetHealth().Heal(100f);
									joueurVictime.GetHealth().Heal(100f);
									_ListeDuels.Remove(unDuel);
									e.Damage.Amount = 0f;
									return;
								}
							}
						}
					}
				}
			}
		}
		#endregion
		
		#region Functions
		
		private void setUpTimerDuels(){
			timer.Repeat(30f, 0, () =>
			{	
				verifierDuels();	
			});
		}
		
		private void verifierDuels(){
			foreach(Duels unDuel in _ListeDuels){
				if(!unDuel.duelEnCours && getTimestamp()-unDuel.timestampDebutDuel>60){
					_ListeDuels.Remove(unDuel);
					break;
				}
			}
		}
		
		private bool isOnline(string pseudo){
			Player joueur = Server.GetPlayerByName(pseudo);
			if(joueur != null){
				foreach(Player joueurOnline in getJoueursOnline()){
					if(joueurOnline.Id == joueur.Id){				
						return true;
					}
				}
			}
			return false;
		}
		
		private List<Player> getJoueursOnline(){
			List<Player> listeJoueursOnline = new List<Player>();
			foreach(Player joueur in Server.AllPlayers){
				if(joueur.Id != 9999999999){
					listeJoueursOnline.Add(joueur);
				}
			}
			return listeJoueursOnline;
		}
		
		private bool isDamageKilling(Player victim, Damage damage){
			bool willBeKilled = false;
			HumanBodyBones humanBodyBones = damage.HitBoxBone;
			if(victim.GetHealth().TorsoHealth.Bones.Contains(humanBodyBones)){
				if(victim.GetHealth().TorsoHealth.CurrentHealth - damage.Amount < 1){
					willBeKilled = true;
				}
			}
			else if(victim.GetHealth().HeadHealth.Bones.Contains(humanBodyBones)){
				if(victim.GetHealth().HeadHealth.CurrentHealth - damage.Amount < 1){
					willBeKilled = true;
				}
			} 
			else if (victim.GetHealth().LegsHealth.Bones.Contains(humanBodyBones)){
				if(victim.GetHealth().TorsoHealth.CurrentHealth + victim.GetHealth().LegsHealth.CurrentHealth - damage.Amount < 1){
					willBeKilled = true;
				}
			}
			else{
				float num = victim.GetHealth().HeadHealth.MaxHealth + victim.GetHealth().TorsoHealth.MaxHealth + victim.GetHealth().LegsHealth.MaxHealth;
				float torsoHealtAfter = victim.GetHealth().HeadHealth.CurrentHealth - damage.Amount* (victim.GetHealth().HeadHealth.MaxHealth / num);
				float headHealtAfter = victim.GetHealth().TorsoHealth.CurrentHealth - damage.Amount*(victim.GetHealth().TorsoHealth.MaxHealth / num);
				if(torsoHealtAfter < 1 || headHealtAfter < 1){
					willBeKilled = true;	
				}	
			}
			return willBeKilled;
		}
		
		private float DistancePointAPointB(float[] pointA, float[] pointB){
			float distance = 0;
			if(pointA != null && pointB != null){
				if(pointA.Length == 3 && pointB.Length == 3){
					Vector3 vector3 = new Vector3(pointA[0] - pointB[0], pointA[1] - pointB[1], pointA[2] - pointB[2]);
					distance = Mathf.Sqrt((float) ((double) vector3.x * (double) vector3.x + (double) vector3.y * (double) vector3.y + (double) vector3.z * (double) vector3.z));	
				}
			}
			return distance;
		}
		
		private float[] getPosition(Player joueur){
			float[]position = positionVide;
			if(joueur != null && joueur.Entity != null){
				position = new float[]{joueur.Entity.Position.x,joueur.Entity.Position.y,joueur.Entity.Position.z};
			}
			return position;
		}
		#endregion
		
		#region Helpers
		private int getTimestamp(){
			return (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;		
		}
		
		private List<Player> getPlayersOnline(){
			List<Player> listPlayersOnline = new List<Player>();
			foreach(Player player in Server.AllPlayers){
				if(player.Id != 9999999999){
					listPlayersOnline.Add(player);
				}
			}
			return listPlayersOnline;
		}
		string GetMessage(string key, ulong userId) => lang.GetMessage(key, this, userId.ToString());
		#endregion
	}
}