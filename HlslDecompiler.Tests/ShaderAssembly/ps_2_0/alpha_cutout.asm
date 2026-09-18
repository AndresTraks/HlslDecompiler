ps_2_0
dcl t0.xy
dcl_2d s0
texld r0, t0.xy, s0
add r1, r0.w, -c0.x
mul r0, r0, c0.y
mov oC0, r0
texkill r1
