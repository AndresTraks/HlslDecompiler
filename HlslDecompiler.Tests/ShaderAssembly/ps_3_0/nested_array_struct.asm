ps_3_0
dcl_texcoord v0.xyz
dp3_sat r0.x, c2.xyz, v0.xyz
mul r0.x, r0.x, c3.x
dp3_sat r0.y, c0.xyz, v0.xyz
mad r0.x, r0.y, c1.x, r0.x
add oC0.xyz, r0.xxx, c4.xyz
mov oC0.w, c4.w
