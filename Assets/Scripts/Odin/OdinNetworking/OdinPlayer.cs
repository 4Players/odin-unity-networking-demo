using System.Linq;
using OdinNative.Odin.Room;
using OdinNetworking;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Odin.OdinNetworking
{
    public class OdinPlayer : OdinNetworkIdentity
    {
        [Header("Player Settings")]
        
        public TextMeshPro playerName;

        public Transform mouthGameObject;

        [OdinSyncVar(hook = nameof(OnNameChanged))]
        public string Name;
        
        public float movementSpeed = 2.0f;
        
        private StarterAssetsInputs _input;
        private CharacterController _characterController;
        private PlayerInput _playerInput;
        private ThirdPersonController _thirdPersonController;

        private float _lastSphereSpawn = 0;

        private Vector2 movement = new Vector2(0, 0);
        private float _nextMovementChange = 0;

        private void Awake()
        {
            _input = GetComponent<StarterAssetsInputs>();
            _characterController = GetComponent<CharacterController>();
            _playerInput = GetComponent<PlayerInput>();   
            _thirdPersonController = GetComponent<ThirdPersonController>();   
        }

        // Start is called before the first frame update
        void Start()
        {

            Debug.Log($"Added player with peer Id: {Peer.Id}");

            Name = $"Player_{Peer.Id}";
        }

        public override Transform GetPlaybackComponentContainer(Room room)
        {
            return mouthGameObject != null ? mouthGameObject : gameObject.transform;
        }

        // Update is called once per frame
        void Update()
        {
            playerName.text = Name;

            if (!IsLocalPlayer())
            {
                return;
            }

            /*if (Time.time > _nextMovementChange)
            {
                _input.MoveInput(new Vector2(Random.Range(-1.0f,1.0f),Random.Range(-1.0f,1.0f)));
                _nextMovementChange = Time.time + Random.Range(1.0f, 5.0f);
            }*/
            

            if (Keyboard.current.eKey.wasReleasedThisFrame)
            {
                SpawnNetworkedObject("Flower", transform.position, Quaternion.identity);
            }

            if (Keyboard.current.rKey.wasReleasedThisFrame)
            {
                if (SpawnedObjects.Count > 0)
                {
                    DestroyNetworkedObject(SpawnedObjects.Last());
                }
            }

            if (_input.jump)
            {
                if (Time.time - _lastSphereSpawn > 1)
                {
                    Vector3 position = transform.position + (Vector3.up * 1.5f);
                    SpawnManagedNetworkedObject("Sphere", position, Quaternion.identity);
                    _lastSphereSpawn = Time.time;
                }
            }
            
            //transform.localPosition += new Vector3(_input.move.x * Time.deltaTime * movementSpeed, 0, _input.move.y * Time.deltaTime * movementSpeed);
        }

        public override void OnStartLocalClient()
        {
            base.OnStartLocalClient();

            _characterController.enabled = true;
            _playerInput.enabled = true;
            _thirdPersonController.enabled = true;
        }


        public void OnNameChanged(string oldValue, string newValue)
        {
            Debug.Log($"NAME CHANGED FROM {oldValue} TO {newValue}");
        }
    }
}

