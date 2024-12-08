vs_4_0
dcl_constantbuffer cb0[3], immediateIndexed
dcl_input v0
dcl_input v1.xyz
dcl_input v2.xy
dcl_output o0
dp4 o0.x, cb0[2], v0
dp3 o0.y, cb0[1].xyz, v1.xyz
dp2 o0.z, cb0[0].xy, v2.xy
mov o0.w, l(4)
ret
