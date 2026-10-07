ps_2_0
def c2, 0, 0, 0, 0
dcl t1.xy
dcl v0.xy
dcl_2d s0
mov r0.w, c2.x
dp2add r0.x, v0.xy, c0.xy, r0.ww
dp2add r0.y, v0.xy, c1.xy, r0.ww
add r0.xy, r0.xy, t1.xy
texld r0, r0.xy, s0
mov oC0, r0
