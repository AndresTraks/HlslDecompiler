vs_4_0
dcl_input v0.xyz
dcl_output o0
dcl_temps 1
dp3 r0.x, v0.xyz, v0.xyz
rsq r0.x, r0.x
mul o0.xyz, r0.xxx, v0.yxz
mov o0.w, l(1)
ret
