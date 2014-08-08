using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public static class PlayerManager {
	// general information
	private static List<PlayerInfo> players = new List<PlayerInfo>();		// list of players
	public static ReadOnlyCollection<PlayerInfo> Players { get { return players.AsReadOnly(); } }

	
	// create a local player
	public static PlayerInfo CreateLocalPlayer(string Name) {
		// create a default player
		PlayerInfo player = new PlayerInfo(Name, "white", CurrentState);
		players.Add(player);
		return player;
	}
	// create a networked player
	public static PlayerInfo CreateNetworkPlayer(string Name) {
		// create a default player
		PlayerInfo player = new PlayerInfo(Name, "white", CurrentState);
		players.Add(player);
		return player;
	}


	// search functions
	// TODO: make this use p.player.tostring's number rather than an arbitrary number
	public static PlayerInfo GetPlayerById( int playerId ){
		foreach (PlayerInfo p in players) {
			if (p.playerId==playerId) {
				return p;
			}
		}
		return null;
	}
	// this will return the first player with the matching name
	public static PlayerInfo GetPlayerByName(string name){
		foreach (PlayerInfo p in players) {
			if (p.name==name) {
				return p;
			}
		}
		return null;
	}
	// returns the player that owns an object, or null
	public static PlayerInfo GetOwner(GameObject obj){
		if(obj && obj.networkView) {
			foreach (PlayerInfo p in players) {
				if(p.ballViewID==obj.networkView.viewID
				   || p.cartViewID==obj.networkView.viewID
				   || p.characterViewID==obj.networkView.viewID){
					return p;
				}
			}
		}
		return null;
	}
	public static PlayerInfo GetPlayerByNetworkPlayer(NetworkPlayer player){
		foreach (PlayerInfo p in players) {
			if (p.player==player) {
				return p;
			}
		}
		return null;
	}
}
