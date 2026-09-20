ps_4_0
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 1
mul r0.x, v0.y, cb0[0].x
div o0.x, v0.x, r0.x
div r0.x, v0.y, cb0[0].y
div o0.y, v0.x, r0.x
div r0.x, v0.z, v0.w
mul o0.z, r0.x, cb0[0].z
mul r0.xy, v0.yw, v0.xz
div o0.w, r0.x, r0.y
ret
