ps_2_0
dcl t0.xy
dcl t1.xy
dcl_2d s0
dcl_2d s1
texld r0, t0.xy, s0
add r1.xy, r0.xy, t1.xy
texld r1, r1.xy, s1
mul r0, r0, r1
mov oC0, r0
