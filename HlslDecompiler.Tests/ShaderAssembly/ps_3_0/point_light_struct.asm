ps_3_0
def c6, 1, 0, 0, 0
dcl_texcoord v0.xyz
add r0.xyz, c3.xyz, -v0.xyz
dp3 r0.x, r0.xyz, r0.xyz
rcp r0.x, r0.x
mov r1.xyz, c5.xyz
mul r0.yzw, r1.xyz, c4.xxx
mul oC0.xyz, r0.xxx, r0.yzw
mov oC0.w, c6.x
