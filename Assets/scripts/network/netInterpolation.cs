using UnityEngine;
using System.Collections;

// individual packets
public struct packet {
	public Vector3 position;
	public Vector3 velocity;
	public Quaternion rotation;
	public int intstamp;
	public double timestamp;
	public double delay;
}

// position buffer things
public struct pb {
	public Vector3 position;
	public Vector3 velocity;
	public Quaternion rotation;
	public double time;
}

/* Doesn't actually interpolate yet - currently just checks previous locations are valid
 * 
 * THIS SCRIPT RELIES ON A CONSTANT Time.fixedDeltaTime
 * 
 * How it works (for when I forget/when other people want to read it):
 * Client sends messages to server about button presses (in another script)
 * Server updates location based on these presses (in another script)
 * Server sends packets back to the client about where they should be from the servers point of view
 * Client checks that it was at that location previously and if not makes adjustments
 * 
 * TODO:
 * Add a bit that corrects velocity for vehicles not owned by the client (otherwise they just jump everywhere!)
 */
public class netInterpolation : MonoBehaviour {
	NetworkViewID viewID;
	packet[] packets;
	pb[] positionBuffer;
	int currentIntstamp;		// number of "current" packet in interpolation
	double currentTimestamp;	// time of recieving "current" packet in interpolation
	//Vector3[] bezPosition;		// co-efs for position interpolation
	//Quaternion[] bezRotation;	// co-efs for rotation interpolation
	/*int noOfPoints = 0;			// no of points we're interpolating from
	float normalizer = 1;		// cache a divisor for FixedUpdate
	// Binomial co-efs
	int[,] BinCoefs = new int[5,5] {
		{1,4,6,4,1},
		{1,3,3,1,0},
		{1,2,1,0,0},
		{1,1,0,0,0},
		{1,0,0,0,0}
	};*/
	bool hasRigidBody;

	// tolerances
	float maxDist = 0.1f;	// maximum distance tolerance (Unity units)
	float maxAngle = 5;		// maximum angle tolerance (degrees) 

	// called to start it
	public void Init(NetworkViewID cartViewID) {
		viewID = cartViewID;
		NetworkView cgt = gameObject.AddComponent("NetworkView") as NetworkView;
		cgt.observed = this;					// track this script
		cgt.viewID = cartViewID;
		cgt.stateSynchronization = NetworkStateSynchronization.Unreliable;

		// check for rigidbody
		hasRigidBody = (gameObject.rigidbody!=null);

		// fixed no of packets to interpolate from (I know, I know...) - this might not even be needed
		packets = new packet[5];
		for(int i=0;i<5;i++) {
			packets[i].position = gameObject.transform.position;
			if(hasRigidBody) packets[i].rotation = gameObject.rigidbody.rotation;
			packets[i].timestamp = Network.time;
			packets[i].intstamp = 0;	// first packet will be 1 so 0 is the null-packet
			packets[i].delay = 0;
		}
		// position buffer reset
		// 10 points since each point is ~20ms apart and each packet is ~50ms apart so 10 points gives us 4 packets resolution
		positionBuffer = new pb[10];
		for(int i=0;i<10;i++) {
			positionBuffer[i].position = gameObject.transform.position;
			if(hasRigidBody) positionBuffer[i].velocity = gameObject.rigidbody.velocity;
			if(hasRigidBody) positionBuffer[i].rotation = gameObject.rigidbody.rotation;
			positionBuffer[i].time = Time.time;
		}
		// reset the vars
		currentIntstamp = 0;
		currentTimestamp = Network.time;
		//bezPosition = new Vector3[5];
		//bezRotation = new Quaternion[5];

		/*debug
		Vector3[] tmpvec = new Vector3[5] {
			new Vector3(0,0,0),
			new Vector3(1,0,0),
			new Vector3(2,7,0),
			new Vector3(1,0,0),
			new Vector3(0,0,0)
		};
		int startPacket = 0;
		noOfPoints = 5-startPacket;
		Debug.Log("START");
		for(int i=startPacket;i<5;i++) {
			bezPosition[i-startPacket] = BinCoefs[startPacket,i-startPacket] * tmpvec[i];
		}
		Debug.Log("lerps:");
		Debug.Log(BezPositionInterpolate(0f));
		Debug.Log(BezPositionInterpolate(0.25f));
		Debug.Log(BezPositionInterpolate(0.5f));
		Debug.Log(BezPositionInterpolate(0.75f));
		Debug.Log(BezPositionInterpolate(1f));
		Debug.Log("END");
		*/
		// need to debug rotation! - probably the lerp's too small
	}

	void OnSerializeNetworkView(BitStream stream, NetworkMessageInfo info) {
		// set up local copies
		Vector3 pPosition;	// don't know why this doesn't have to be defined :S
		Vector3 pVelocity = new Vector3();
		Quaternion pRotation = new Quaternion();
		int pIntstamp;

		// check if we need to update or if we are being updated (ie server vs client)
		if (stream.isWriting) {
			// server
			// update stuff
			currentIntstamp++;

			// copy for streaming
			pPosition = gameObject.transform.position;
			if(hasRigidBody) pRotation = gameObject.rigidbody.rotation;
			pIntstamp = currentIntstamp;		// this will tick over after about half a year, so don't leave it running too long

			// set stream
			stream.Serialize(ref pPosition);
			if(hasRigidBody) stream.Serialize(ref pVelocity);
			if(hasRigidBody) stream.Serialize(ref pRotation);
			stream.Serialize(ref pIntstamp);

		} else {
			// client
			pPosition = new Vector3();
			//if(hasRigidBody) pRotation = new Quaternion();
			pIntstamp = 0;

			// get stream
			stream.Serialize(ref pPosition);
			if(hasRigidBody) stream.Serialize(ref pVelocity);
			if(hasRigidBody) stream.Serialize(ref pRotation);
			stream.Serialize(ref pIntstamp);

			// convert to a packet
			packet newPacket = new packet();
			newPacket.position = pPosition;
			newPacket.rotation = pRotation;
			newPacket.timestamp = info.timestamp;
			newPacket.intstamp = pIntstamp;
			newPacket.delay = Network.time - info.timestamp;

			// do the checking
			if (packets[4].intstamp==0) {
				// no previous data so just set it and move along
				gameObject.transform.position = newPacket.position;
				if(hasRigidBody) gameObject.rigidbody.rotation = newPacket.rotation;

			} else if (packets[4].intstamp<newPacket.intstamp-1) {
				// did we lose any packets?
				// tib siht :ODOT

			} else {
				// everythings normal so interpolate away!
				// remember - previous history is correct since we corrected for it
				double bufferTime = Time.time - newPacket.delay;

				//Debug.Log(newPacket.delay);
				//Debug.Log(bufferTime);

				// find the relevant position buffer
				int pbIndex = -1;
				for(int i=9;i>=0;i--) {
					// check timestamp
					if (positionBuffer[i].time<bufferTime) {
						pbIndex = i;
						break;
					}
				}

				// make sure it misses the first pass
				// special case pbIndex==9 is safe to ignore since we've updated all before it
				if (pbIndex!=-1 && pbIndex!=9) {
					// position check
					// lerp it a bit
					Vector3 whereItWas = Vector3.Lerp(positionBuffer[pbIndex].position,
					                                  positionBuffer[pbIndex+1].position,
					                                  (float)(bufferTime - positionBuffer[pbIndex].time) / Time.fixedDeltaTime);	// dividing by Time.fixedDeltaTime since that shouldn't change - right?
					
					// check it was within a tolerance (sqr faster since no sqrt)
					if (Vector3.SqrMagnitude(whereItWas-newPacket.position)>maxDist*maxDist) {
						// correct it by shifting the current location by the difference in position (not a good idea but should work as a first pass)
						Vector3 deltaPosition = newPacket.position - whereItWas;
						gameObject.transform.position += deltaPosition;
						
						// correct the previous positions aswell
						for(int i=pbIndex+1;i==9;i++) {
							positionBuffer[i].position += deltaPosition;
						}
					}

					if(hasRigidBody) {
						// rotation check
						// lerp it a bit (or slerp for speed?)
						Quaternion whereItRot = Quaternion.Lerp(positionBuffer[pbIndex].rotation,
						                                  positionBuffer[pbIndex+1].rotation,
						                                  (float)(bufferTime - positionBuffer[pbIndex].time) / Time.fixedDeltaTime);	// dividing by Time.fixedDeltaTime since that shouldn't change - right?
						
						// check it was within a tolerance (degrees for some reason and seems to be always positive)
						if (Quaternion.Angle(whereItRot,newPacket.rotation)>maxAngle) {
							// correct it by rotating the current rotation by the difference in rotation (not a good idea but should work as a first pass)
							Quaternion deltaRotation = newPacket.rotation * Quaternion.Inverse(whereItRot);
							gameObject.rigidbody.rotation = deltaRotation * gameObject.rigidbody.rotation;
							
							// correct the previous positions aswell
							for(int i=pbIndex+1;i==9;i++) {
								positionBuffer[i].rotation = deltaRotation * positionBuffer[i].rotation;
							}
						}
						
						// velocity check
						// lerp it a bit
						Vector3 whereItVel = Vector3.Lerp(positionBuffer[pbIndex].velocity,
						                                  positionBuffer[pbIndex+1].velocity,
						                                  (float)(bufferTime - positionBuffer[pbIndex].time) / Time.fixedDeltaTime);	// dividing by Time.fixedDeltaTime since that shouldn't change - right?
						
						// check it was within a tolerance (sqr faster since no sqrt)
						if (Vector3.SqrMagnitude(whereItVel-newPacket.velocity)>maxDist*maxDist) {
							// correct it by shifting the current location by the difference in position (not a good idea but should work as a first pass)
							Vector3 deltaVelocity = newPacket.velocity - whereItVel;
							gameObject.rigidbody.velocity += deltaVelocity;
							
							// correct the previous positions aswell
							for(int i=pbIndex+1;i==9;i++) {
								positionBuffer[i].velocity += deltaVelocity;
							}
						}
					}
				}
			}

			// add the packet to the list
			// ordering: 0=oldest,4=latest
			for(int i=0;i<5;i++) {
				// check timestamp
				if (i!=4 && packets[i+1].intstamp < pIntstamp) {
					packets[i] = packets[i+1];
				} else {
					packets[i] = newPacket;
					break;
				}
			}

			// set up the stuff needed to interpolate
			//Debug.Log(Network.time - packets[0].timestamp);
			//if (packets[1].timestamp-packets[0].timestamp>0.01) Debug.Log("Balls");
			/*string tmpstr = "";
			tmpstr += "0";
			tmpstr += "," + (packets[1].intstamp-packets[0].intstamp).ToString();
			tmpstr += "," + (packets[2].intstamp-packets[0].intstamp).ToString();
			tmpstr += "," + (packets[3].intstamp-packets[0].intstamp).ToString();
			tmpstr += "," + (packets[4].intstamp-packets[0].intstamp).ToString();
			if (tmpstr!="0,1,2,3,4") {
				Debug.LogError(tmpstr);
			}*/
			// set us to the last one
			currentIntstamp = packets[0].intstamp;
			// set time
			currentTimestamp = Network.time;

			// set up the Bezier curves
			// first find out the starting packet
			/*int startPacket = 0;
			for(int i=0;i<4;i++) {
				if (packets[i+1].timestamp > Network.time-0.1) {
					startPacket = i;
					break;
				}
			}
			// set the co-efs
			for(int i=startPacket;i<5;i++) {
				bezPosition[i-startPacket] = BinCoefs[startPacket,i-startPacket] * packets[i].position;
				//bezRotation[i-startPacket] = BinCoefs[startPacket,i-startPacket] * packets[i].rotation;
				//if(hasRigidBody) bezRotation[i-startPacket] = packets[i].rotation;	// need to somehow lerp it here instead for performance
			}
			// set no of point
			noOfPoints = 5-startPacket;
			// do division now for speed
			normalizer = (float)(1/(packets[4].timestamp-packets[startPacket].timestamp));
			*/
		}
	}

	// update the position buffer
	void FixedUpdate() {
		// shift everything down 1
		// ordering: 0=oldest,10=latest
		for(int i=0;i<9;i++) {
			positionBuffer[i] = positionBuffer[i+1];
		}
		// add new position to the buffer
		pb newPb = new pb();
		newPb.position = gameObject.transform.position;
		if(hasRigidBody) newPb.rotation = gameObject.rigidbody.rotation;
		if(hasRigidBody) newPb.velocity = gameObject.rigidbody.velocity;
		newPb.time = Time.time;
		positionBuffer[9] = newPb;
	}

	// lerp it
	/*void FixedUpdate() {
		if (Network.isClient) {
			if (noOfPoints>1) {
				float t = (float)(Network.time - currentTimestamp);	// shouldn't overflow
				t = t*normalizer;	// normalize the timestep - not the best idea...
				gameObject.transform.position = PositionInterpolate(t);
				//if(hasRigidBody) gameObject.rigidbody.rotation = BezRotationInterpolate(t);
			}
		}
	}*/

	// get the interpolated position
	/*Vector3 BezPositionInterpolate(float t) {
		Vector3 tmp = new Vector3();
		for(int i=0;i<noOfPoints;i++) {
			tmp += bezPosition[i] * QuickPower(t,i) * QuickPower(1-t,noOfPoints-1-i);
			//Debug.Log(QuickPower(t,i) * QuickPower(1-t,noOfPoints-i));
		}
		//if(viewID.ToString()=="AllocatedID: 1") Debug.Log(tmp);
		return tmp;
	}
	// get the interpolated rotation
	Quaternion BezRotationInterpolate(float t) {
		Quaternion tmp = new Quaternion();
		for(int i=0;i<noOfPoints;i++) {
			// lerp not slerp as it's faster
			tmp *= Quaternion.Lerp(Quaternion.identity, bezRotation[i], BinCoefs[5-noOfPoints,i] * QuickPower(t,i) * QuickPower(1-t,noOfPoints-1-i));
		}
		return tmp;
	}
	// faster than math.pow I think
	float QuickPower(float t, int n) {
		if(n==0) return 1;
		if(t==0) return 0;
		float tmp=1;
		for(int i=n;i!=0;i--) {
			tmp *= t;
		}
		return tmp;
	}*/
}
