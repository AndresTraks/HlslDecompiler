ps_4_0
dcl_constantbuffer cb0[4], immediateIndexed
dcl_input_ps linear v0.xyz
dcl_output o0
dcl_temps 2
add r0.xyz, -cb0[0].xyz, cb0[1].xyz
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul r0.xyz, r0.www, r0.xyz
dp3 r0.w, v0.xyz, v0.xyz
rsq r0.w, r0.w
mul r1.xyz, r0.www, v0.xyz
dp3_sat r0.x, r1.xyz, r0.xyz
dp3_sat r0.y, r1.xyz, -cb0[0].xyz
log r0.x, r0.x
mul r0.x, r0.x, cb0[1].w
exp r0.x, r0.x
mul r1, r0.x, cb0[3]
mad o0, cb0[2], r0.y, r1
ret
