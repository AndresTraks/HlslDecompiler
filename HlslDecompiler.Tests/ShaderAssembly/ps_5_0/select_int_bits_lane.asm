ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[2], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
itof r0.xyz, cb0[0].xzw
mul r0.yz, r0.yz, l(0, 0.5, 0.5, 0)
lt r1, l(0, 0, 0, 0), v0
movc o0.xzw, r1.xzw, r0.xyz, cb0[1].xzw
movc o0.y, r1.y, cb0[0].y, cb0[1].y
ret
