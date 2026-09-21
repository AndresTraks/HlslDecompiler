ps_3_0
dcl_texcoord v0.xyz
mov r0.xyz, c0.xyz
mov r1.xyz, c2.xyz
mad r0.xyz, r0.xyz, r1.xyz, c1.xyz
mul oC0.xyz, r0.xyz, v0.xyz
mov oC0.w, c3.x
