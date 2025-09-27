ps_3_0
def c1, 0, -1, 1, 0
dcl_texcoord v0.xy
mov r0.xy, c1.xy
add r0.xy, r0.xy, c0.xx
abs r0.xy, r0.xy
mov r0.zw, -r0.xy
mov r0.xy, -r0.xy
add r0.xy, r0.xy, r0.zw
cmp r0.xy, r0.xy, c1.zz, c1.ww
mul r1.xy, v0.xy, r0.xy
add oC0, r1.x, r1.y
