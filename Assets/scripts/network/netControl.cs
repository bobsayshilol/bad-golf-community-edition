using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class netControl : MonoBehaviour {
	networkVariables nvs;
	PlayerInfo myInfo;
	Transform cameraParentTransform;
	GameObject localBallAnalog;
	bool honking = false;
	bool loaded = false;

	// called when first added
	void Start() {
		// get variables we need
		nvs = GetComponent("networkVariables") as networkVariables;
		myInfo = nvs.myInfo;

		localBallAnalog = new GameObject ();
	}

	// called when the level has been loaded
	void Loaded() {
	}

	void Update() {
		// only do key presses if it's not paused
		if (!myInfo.playerIsPaused && myInfo.currentMode==0) {
			// HONK
			if (Input.GetKeyDown(KeyCode.Q) && !honking) {
				honking = true;
				networkView.RPC("IHonked", RPCMode.Others, myInfo.player);
				Honk(myInfo.cartGameObject);
			} else if(Input.GetKeyUp(KeyCode.Q)) {
				honking = false;
			}
		}
	}

	// UPDATE ALL THE FIZIKS!
	void FixedUpdate () {
		// if in buggy then update what they be pressing
		if (!myInfo.playerIsPaused && myInfo.currentMode==0) {
			// add own fiziks
			#if !UNITY_EDITOR && (UNITY_IPHONE || UNITY_ANDROID)
				myInfo.h = Input.acceleration.x;
				myInfo.v = Input.acceleration.y + .5f;
			#else
				myInfo.h = Input.GetAxis("Horizontal");
				myInfo.v = Input.GetAxis("Vertical");
			#endif

		} else {
			// paused so don't move
			myInfo.h = 0f;
			myInfo.v = 0f;
		}

		// update the fiziks AFTER getting local key input
		// this moves all the carts
		foreach (PlayerInfo p in nvs.players) {
			// if in buggy
			if (p.currentMode==0) {
				p.carController.Move(p.h,p.v);
				
			} else if (p.currentMode==1) {	// if in ball mode
				p.carController.Move(0f,0f);
			}
		}

		// send server our input if we're a client - only to the server
		if (Network.isClient) {
			networkView.RPC("KartMovement", RPCMode.Server, myInfo.h, myInfo.v, myInfo.playerId);
		} else {
			// goodbye bandwidth - but is there a better way?
			/*foreach (PlayerInfo p in nvs.players) {
				networkView.RPC("KartMovement", RPCMode.Others, p.h, p.v, p.playerId);
			}*/
		}
	}
	
	/* if we're a server then update them all - can't use this
	void OnSerializeNetworkView(BitStream stream, NetworkMessageInfo info) {
		// check if we need to update or if we are being updated (ie server vs client)
		if (stream.isWriting) {
			// server

			* "packet" format:
			 * int playerId
			 * float h
			 * float v
			*
			foreach (PlayerInfo p in nvs.players) {
				stream.Serialize(ref p.playerId);
				stream.Serialize(ref p.h);
				stream.Serialize(ref p.v);
			}
			
		} else {
			// client

			// "packet"
			int playerId = 0;
			float h = 0;
			float v = 0;

			// need to go through each player but we don't know the order, so just loop playercount times
			for(int i=0;i<nvs.players.Count;i++) {
				stream.Serialize(ref playerId);
				stream.Serialize(ref h);
				stream.Serialize(ref v);

				// make sure it's not us
				if (playerId==myInfo.playerId) continue;
				Debug.Log(playerId);

				PlayerInfo p = nvs.getPlayerById(playerId);
				if (p!=null) {
					p.h = h;
					p.v = v;
				}
			}
		}
	}*/

	void switchToBall(){
		// if in buggy
		if (myInfo.currentMode==0) {
			myInfo.currentMode = 1;
			networkView.RPC ("PlayerSwap", RPCMode.Others, myInfo.currentMode, myInfo.player);	//to ball
			//stop cart
			myInfo.cartGameObject.rigidbody.velocity = Vector3.zero;
			myInfo.cartGameObject.rigidbody.angularVelocity = Vector3.zero;
			// set them at golf ball
			myInfo.ballGameObject.transform.rotation = Quaternion.identity;

			localBallAnalog.transform.position = myInfo.ballGameObject.transform.position;	//hack_answers
			localBallAnalog.transform.rotation = myInfo.ballGameObject.transform.rotation;	//hack_answers
			localBallAnalog.transform.localScale = myInfo.ballGameObject.transform.localScale;	//hack_answers
			//myInfo.characterGameObject.transform.parent = myInfo.ballGameObject.transform;
			myInfo.characterGameObject.transform.parent = localBallAnalog.transform;

			// find the pin - might have moved
			GameObject pin = GameObject.Find("winningPole");
			myInfo.ballGameObject.transform.rotation = Quaternion.LookRotation((pin.transform.position - myInfo.ballGameObject.transform.position) - new Vector3(0, pin.transform.position.y - myInfo.ballGameObject.transform.position.y,0));	
			myInfo.characterGameObject.transform.localPosition = new Vector3(1.7f,-.2f,0);
			myInfo.characterGameObject.transform.localRotation = new Quaternion(0f, -Mathf.PI/2, 0f, 1f);

			localBallAnalog.transform.rotation = myInfo.ballGameObject.transform.rotation;
			// lock golf ball
			myInfo.ballGameObject.rigidbody.constraints = RigidbodyConstraints.FreezeAll;
			//*/ move camera - HACKY (as if the rest of this isn't)
			GameObject buggyCam = nvs.myCam.gameObject;
			(buggyCam.GetComponent("FollowPlayerScript") as FollowPlayerScript).enabled = false;
			cameraParentTransform = buggyCam.transform.parent;	// keep a reference for later
			//buggyCam.transform.parent = myInfo.ballGameObject.transform;
			buggyCam.transform.parent = localBallAnalog.transform;	//hack_answers
			buggyCam.transform.rotation = Quaternion.identity;	// is this line needed?
			buggyCam.transform.localPosition = new Vector3(-6,4,0);
			//buggyCam.transform.rotation = Quaternion.LookRotation(myInfo.ballGameObject.transform.position - buggyCam.transform.position);
			buggyCam.transform.localRotation = Quaternion.identity;

			//change animation
			myInfo.characterGameObject.animation.Play("golfIdle",PlayMode.StopAll);
		}
	}

	void switchToCart(){
		myInfo.currentMode = 0;
		networkView.RPC ("PlayerSwap", RPCMode.Others, myInfo.currentMode, myInfo.player);	//to cart
		// set them in buggy
		myInfo.characterGameObject.transform.parent = myInfo.cartGameObject.transform;
		myInfo.characterGameObject.transform.localPosition = new Vector3(0,0,0);
		myInfo.characterGameObject.transform.rotation = myInfo.cartGameObject.transform.rotation;
		// unlock golf ball
		myInfo.ballGameObject.rigidbody.constraints = RigidbodyConstraints.None;
		//*/ move camera - HACKY
		GameObject buggyCam = nvs.myCam.gameObject;
		buggyCam.transform.parent = cameraParentTransform;	// put it back
		
		(buggyCam.GetComponent("FollowPlayerScript") as FollowPlayerScript).enabled = true;

		//change animation
		myInfo.characterGameObject.animation.Play("driveIdle",PlayMode.StopAll);

		(GetComponent ("netTransferToSwing") as netTransferToSwing).enabled = true;
	}

	// honks
	void Honk(GameObject cart) {
		SoundManager.Get().playSfx3d(cart, "horn1", 5, 500, 1);
	}
	
	// honks
	[RPC]
	void IHonked(NetworkPlayer player) {
		// find the player
		PlayerInfo p = nvs.getPlayerByNetworkPlayer(player);
		// if it exists do the thing
		if(p!=null) {
			Honk(p.cartGameObject);
		}
	}

	// change player mode - somehow use switchToCart aswell
	[RPC]
	void PlayerSwap(int newMode, NetworkPlayer player) {
		// find the player
		PlayerInfo p = nvs.getPlayerByNetworkPlayer(player);
		// if it exists do the thing
		if(p!=null) {
			p.currentMode = newMode;
			if (p.currentMode==0) {			// if they're now in a buggy
				// set them in buggy
				p.characterGameObject.transform.parent = p.cartGameObject.transform;
				p.characterGameObject.transform.localPosition = new Vector3(0,0,0);
				p.characterGameObject.transform.rotation = p.cartGameObject.transform.rotation;
				// unlock golf ball
				p.ballGameObject.rigidbody.constraints = RigidbodyConstraints.None;
				//change animation
				p.characterGameObject.animation.Play("driveIdle",PlayMode.StopAll);
			} else if (p.currentMode==1) {	// if they're now at golf ball
				//stop cart
				p.cartGameObject.rigidbody.velocity = Vector3.zero;
				p.cartGameObject.rigidbody.angularVelocity = Vector3.zero;
				// set them at golf ball
				p.characterGameObject.transform.parent = p.ballGameObject.transform;
				// find the pin - might have moved
				GameObject pin = GameObject.Find("winningPole");
				p.ballGameObject.transform.rotation = Quaternion.LookRotation((pin.transform.position - p.ballGameObject.transform.position) - new Vector3(0, pin.transform.position.y - p.ballGameObject.transform.position.y,0));	
				p.characterGameObject.transform.localPosition = new Vector3(1.7f,-.2f,0);
				p.characterGameObject.transform.localRotation = Quaternion.identity * new Quaternion(0f, -Mathf.PI/2, 0f, 1f);
				// lock golf ball
				p.ballGameObject.rigidbody.constraints = RigidbodyConstraints.FreezeAll;
				//change animation
				p.characterGameObject.animation.Play("golfIdle",PlayMode.StopAll);
			}

			// reset keyboard buffer
			p.h = 0f;
			p.v = 0f;
		}
	}

	// updates what a player is currenly doing
	[RPC]
	public void KartMovement(float h, float v, int playerID, NetworkMessageInfo info) {
		if (nvs) {
			// get the player
			PlayerInfo p = nvs.getPlayerByNetworkPlayer(info.sender); // using playerID breaks it :S
			// check it exists
			if (p!=null) {
				//if(Network.isServer) {Debug.LogError(new Vector2(p.h,p.v));Debug.LogError(playerID!=myInfo.playerId);}
				p.h = h;
				p.v = v;
			}
		}
	}
}
