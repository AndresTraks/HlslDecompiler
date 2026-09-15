ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_input_ps linear v0.xyz
dcl_output o0
dcl_temps 1
add r0.x, v0.y, cb0[0].z
add o0.x, -r0.x, v0.x
dp2 r0.x, cb0[0].yz, v0.yz
mad o0.w, cb0[0].x, v0.x, -r0.x
add r0.xy, v0.yy, -cb0[0].zw
add o0.y, -r0.x, v0.x
add o0.z, r0.y, v0.x
ret
