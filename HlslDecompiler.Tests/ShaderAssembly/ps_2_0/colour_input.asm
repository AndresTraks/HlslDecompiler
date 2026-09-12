ps_2_0
dcl t0.xy
dcl v0
dcl_2d s0
texld r0, t0.xy, s0
lrp r1, c0.x, v0, r0
add_sat r0, r0, -v0
mad r0, r1, c0, r0
mov oC0, r0
