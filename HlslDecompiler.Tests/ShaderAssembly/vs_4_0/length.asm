vs_4_0
dcl_input v0
dcl_input v1.xyz
dcl_input v2
dcl_output o0
dcl_temps 1
add r0, v2, l(2, 2, 2, 2)
dp4 r0.x, r0, r0
sqrt r0.x, r0.x
mul o0.w, r0.x, l(-5)
dp2 r0.x, v2.xy, v2.xy
sqrt r0.x, r0.x
mov o0.z, -r0.x
dp4 r0.x, v0, v0
sqrt o0.x, r0.x
dp3 r0.x, v1.xyz, v1.xyz
sqrt o0.y, r0.x
ret
