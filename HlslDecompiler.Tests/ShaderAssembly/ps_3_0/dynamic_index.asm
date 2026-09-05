ps_3_0
def c1, 0, -1, 1, 0
dcl_texcoord v0.xy
mov r0.xy, c1.xy
add r0.xy, r0.xy, c0.xx
cmp r0.xy, -r0.xy_abs, c1.zz, c1.ww
dp2add r0.x, v0.xy, r0.xy, c1.ww
mov oC0, r0.x
