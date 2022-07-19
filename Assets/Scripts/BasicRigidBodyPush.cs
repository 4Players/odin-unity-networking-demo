using Odin.OdinNetworking;
using Odin.OdinNetworking.Messages;
using UnityEngine;

public class BasicRigidBodyPush : MonoBehaviour
{
	public LayerMask pushLayers;
	public bool canPush;
	[Range(0.5f, 5f)] public float strength = 1.1f;
	protected float _lastSent;

	private void OnControllerColliderHit(ControllerColliderHit hit)
	{
		if (canPush) PushRigidBodies(hit);
	}

	private void PushRigidBodies(ControllerColliderHit hit)
	{
		// https://docs.unity3d.com/ScriptReference/CharacterController.OnControllerColliderHit.html

		// make sure we hit a non kinematic rigidbody
		Rigidbody body = hit.collider.attachedRigidbody;
		if (body == null) return;

		// make sure we only push desired layer(s)
		var bodyLayerMask = 1 << body.gameObject.layer;
		if ((bodyLayerMask & pushLayers.value) == 0) return;

		// We dont want to push objects below us
		if (hit.moveDirection.y < -0.3f) return;

		// Calculate push direction from move direction, horizontal motion only
		Vector3 pushDir = new Vector3(hit.moveDirection.x, 0.0f, hit.moveDirection.z);

		// Apply the push and take strength into account
		body.AddForce(pushDir * strength, ForceMode.Impulse);
	}
	
	private void PushRigidBodiesOld(ControllerColliderHit hit)
	{
		// https://docs.unity3d.com/ScriptReference/CharacterController.OnControllerColliderHit.html

		// make sure we hit a non kinematic rigidbody
		Rigidbody body = hit.collider.attachedRigidbody;
		if (body == null) return;

		// make sure we only push desired layer(s)
		var bodyLayerMask = 1 << body.gameObject.layer;
		if ((bodyLayerMask & pushLayers.value) == 0) return;

		// We dont want to push objects below us
		if (hit.moveDirection.y < -0.3f) return;

		OdinNetworkIdentity identity = GetComponent<OdinNetworkIdentity>();
		if (!identity)
		{
			return;
		}

		// Calculate push direction from move direction, horizontal motion only
		Vector3 pushDir = new Vector3(hit.moveDirection.x, 0.0f, hit.moveDirection.z);

		if (identity.IsHost)
		{
			// Apply the push and take strength into account
			body.AddForce(pushDir * strength, ForceMode.Impulse);			
		}
		else
		{
			Debug.Log(pushDir);
			if (Time.time - _lastSent > 0.1)
			{
				OdinNetworkedObject worldItem = OdinNetworkManager.Instance.World.GetNetworkObject(hit.collider.gameObject);
				if (worldItem)
				{
					OdinCommandMessage command = new OdinCommandMessage("SetForce");
					command.SetValue("ObjectId", worldItem.ObjectId);
					command.SetValue("PushDir", pushDir * strength);
					OdinNetworkManager.Instance.SendCommand(command);
				}

				_lastSent = Time.time;
			}
		}
	}
}