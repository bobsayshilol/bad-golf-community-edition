using UnityEngine;
using System.Collections;


// different states the player can be in
public enum PlayerState { Cart, Ball, Spectator };


// a class that contains all information on a player
public abstract class PlayerInfo {
	// general information
	public string name { get; protected set; }							// name
	public string color { get; protected set; }							// player color
	public int playerID { get; protected set; }							// player ID
	public int score { get; protected set; }							// player score
	public PlayerState currentState { get; protected set; }				// current State of the player

	// cart
	public GameObject cartGameObject { get; protected set; }			// GameObject of the cart
	public string cartModel { get; protected set; }						// model of the cart

	// ball
	public GameObject ballGameObject { get; protected set; }			// GameObject of the ball
	public string ballModel { get; protected set; }						// model of the ball

	// character
	public GameObject characterGameObject { get; protected set; }		// GameObject of the character
	public string characterModel { get; protected set; }				// model of the character


	// add cart
	protected void _updateCart(string CartModel, GameObject CartGameObject) {
		cartModel = CartModel;
		cartGameObject = CartGameObject;
	}
	// TODO: this bit
	protected void _destroyCart() {}
}

// network version of PlayerInfo
public class NetworkPlayerInfo : PlayerInfo {
	// network-only information
	public NetworkPlayer player { get; protected set; }				// player
	public NetworkViewID cartViewID { get; protected set; }			// NetworkViewID of the cart
	public NetworkViewID ballViewID { get; protected set; }			// NetworkViewID of the ball
	public NetworkViewID characterViewID { get; protected set; }		// NetworkViewID of the character
	
	// standard constructor
	public NetworkPlayerInfo(string Name, string Color, PlayerState CurrentState, NetworkPlayer Player) {
		name = Name;
		color = Color;
		currentState = CurrentState;
		player = Player;
	}

	// add/update cart
	public void UpdateCart(string CartModel, NetworkViewID CartViewID) {
		// TODO
	}
}

// local version of PlayerInfo
public class LocalPlayerInfo : PlayerInfo {
	// local-only information
	public GameObject cameraObject;				//The camera following this player; not UI cam
	public GameObject uiContainer;				//The UI elements (HUD) for this player
	public CarController carController;			//The movement script for this player's buggy
	
	
	// do these need to be net-sunk aswell?
	public bool playerIsBusy   = false;			// player is engaged in an uninteruptable action
	public bool playerIsPaused = false;			// player is paused
	public float v;								//player accelleration/brake input
	public float h;								//player steering input

	
	// standard constructor
	public LocalPlayerInfo(string Name, string Color, PlayerState CurrentState) {
		name = Name;
		color = Color;
		currentState = CurrentState;
	}

	// add/update cart
	public void UpdateCart(string CartModel) {
		_destroyCart();
		// TODO: create
		_updateCart(CartModel);
	}
}
