# ODIN Networking

This is a work-in-progress project of leveraging [ODIN by 4Players](https://www.4players.io/odin) for a real-time client-server based multiplayer 
solution.

ODIN Networking layers above ODIN and leverages message pipeline that ODIN provides to sync player position, animation and managed objects with
other users. In short, by just dropping a script on your player object and joining an ODIN room you have built a real-time multiplayer application
where all users see all other users and can hear their voice. Users can interact with objects provided by the world (i.e. added at design time) and
can spawn new objects to the world.

This demo project is built on top of Unity Third Person Controller project to demonstrate the ease of use.

In `Packages/io.fourplayers.odin` the ODIN SDK is bundled. The Assets folder contains these folders that are of interest:

- `Assets/Scripts/Odin/Audio` in this folder scripts for audio occlusion are provided as Unity does not support audio occlusion out of the box
- `Assets/Scripts/Odin/OdinNetworking`` is the code the ODIN Networking SDK.
- `Assets/Scripts/Odin/` contains a couple of scripts like a simple "Player" script. These scripts leverage base classes from ODIN Networking.

**Please note**: The ODIN Audio and the ODIN Networking folders will soon be bundled with the ODIN SDK. 

## Documentation

You'll find complete documentation for the ODIN Networking SDK [here](Assets/Scripts/Odin/OdinNetworking/README.md).

## ODIN SDK

Learn more about ODIN in general and its features (which are leveraged by OdinNetworking) in our [developer documentation](https://www.4players.io/odin).

## Getting Started

Clone the project (it uses LFS so downloading it from Github might not work) and start it in the Unity Editor. The project is created with 
`Unity 2021.3.0f1` but should work in earlier or later versions, too.

Open the scene `Scenes/OdinNetworking` (its the same as the templates `Playground` scene but adapted to multiplayer experience with OdinNetworking) and
press Play. 

The scene comes with a **bundled access key**. If more users are working with the same demo, you might face some issues with people connected that you dont know. Either enjoy it, or set your own access key in the `Odin Handler` Game Object in the scene. How to do that is described in detail in our
[Unity manual](https://www.4players.io/odin/sdk/unity/manual/odineditorconfig/#client-authentication).

Build the player, distribute to one of your team members or collegues and everyone starting the client will be immediately visible in the world (even in the
Unity Editor) and you can interact and chat with them.

## Controls

You can change the body color in real time using the controls in the bottom left, as well as your name. By pressing the `SPACE` key the avatar will jump
but also will release a managed object that has a built-in time to change its color. Notice how that color syncs to all other clients as well.

Pressing the `R` key will place a flower, which is an unmanaged object but will also be replicated accross the network. Last but not least, pressing the `L` key will toggle the worlds light for everyone. Each of these features demonstrates one core concept of ODIN Networking:

- Spawning a managed object
- Spawning am unmanaged object
- Sending a command

Have a look at the `Player.cs`, `Sphere.cs` and `DemoWorld.cs` script to learn how that works and how easy it is to build these interactive worlds.

Check out the documentation for the ODIN Networking and learn more about how everything works and how to use it in your own application: 
[ODIN Networking Documentation](Assets/Scripts/Odin/OdinNetworking/README.md).

