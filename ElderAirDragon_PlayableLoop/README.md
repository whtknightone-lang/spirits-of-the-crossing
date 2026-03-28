
# Elder Air Dragon Playable Loop

Unity-ready scaffold for an Elder Air Dragon player loop.

## Core loop
- Fly through the world
- Build wind charge while moving
- Use gust, spiral ascent, and resonance pulse
- Regain harmony by gliding smoothly
- Lose integrity when overexerting abilities
- Recover through calm aerial flow

## Suggested setup
1. Import these scripts into your Unity project
2. Create a Dragon prefab with:
   - CharacterController
   - ElderAirDragonController
   - ElderAirDragonStats
   - ElderAirDragonAbilities
3. Add a camera with DragonCameraFollow
4. Add a canvas with DragonHUD
5. Add ElderAirDragonGameLoop to an empty GameObject in scene

## Controls
- WASD: steer
- Space: ascend
- Left Shift: dive / accelerate
- Q: gust burst
- E: spiral ascent
- R: resonance pulse
- F: glide meditation / calm state

## Notes
This is a structural gameplay scaffold, not a finished art-authored scene.
