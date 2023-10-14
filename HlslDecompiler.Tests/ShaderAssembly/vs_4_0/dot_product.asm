vs_4_0
dcl_constantbuffer cb0[3], immediateIndexed
dcl_input v0.xyzw
dcl_input v1.xyz
dcl_input v2.xy
dcl_output o0.xyzw
dp4 o0.x, cb0[2].xyzw, v0.xyzw
dp3 o0.y, cb0[1].xyzx, v1.xyzx
dp2 o0.z, cb0[0].xyxx, v2.xyxx
mov o0.w, l(4.000000)
ret
