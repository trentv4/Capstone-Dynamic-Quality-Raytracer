# Project: Capstone - Dynamic Quality Raytracer

> Design document by Trent VanSlyke 
>
> Initial draft: 9/10/2022
>
> [Github link](https://github.com/trentv4/Capstone-Dynamic-Quality-Raytracer)

# Overview

The purpose of this project is to explore how raytracing can be used the quality of certain effects on top of a traditionally rendered and lit scene, with a focus on scenes of nature. The goal is to develop a system that will dynamically observe the scene rendered in both simplistic lighitng as well as raytraced lighting, and then observe the difference between the frames. The renderer will then dynamically guess for the next frame if a certain segment of the framebuffer can be drawn with a lower quality raytrace or no raytrace at all with no loss in visual fidelity. 

### Task List:

- [ ] Implement a traditional renderer
  - [ ] Load models
  - [ ] Draw models unlit
  - [ ] Apply PBR materials to a model
  - [ ] Full BDRF lighting system
- [ ] Implement a raytracing lighting engine
  - [ ] Implement functional raytracer
  - [ ] Allow variable ray counts across the frame
  - [ ] Optimize to run in real-time
- [ ] Design raytrace monitor
  - [ ] Implement quality difference analysis function 
  - [ ] Create a curve for percent-difference vs next-frame-rays 
