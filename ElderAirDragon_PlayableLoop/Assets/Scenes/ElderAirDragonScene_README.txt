
Suggested Unity scene setup

1. Create a DragonPlayer GameObject
   - CharacterController
   - ElderAirDragonStats
   - ElderAirDragonController
   - ElderAirDragonAbilities

2. Create a Main Camera
   - DragonCameraFollow
   - Set target to DragonPlayer

3. Create a Systems GameObject
   - ElderAirDragonGameLoop

4. Create a UI GameObject
   - DragonHUD

5. Optional world setup
   - floating islands
   - wind ring colliders
   - storm zones with rigidbodies
   - ambient sky sphere

Gameplay loop:
Fly -> build charge -> use abilities -> manage harmony/integrity -> meditate to recover -> repeat
