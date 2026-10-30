ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 1
mul r0.x, v0.x, v0.x
add r0.yz, -v0.xy, l(0, 1, 1, 0)
mul r0.yz, r0.yz, r0.yz
mul r0.xyz, r0.xyz, v0.xxy
mul r0.yz, r0.yz, l(0, 3, 3, 0)
mul o0.y, r0.z, r0.x
mul o0.x, r0.z, r0.y
mov o0.z, r0.y
mov o0.w, v0.y
ret
