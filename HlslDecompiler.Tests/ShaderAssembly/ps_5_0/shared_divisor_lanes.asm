ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
mul r0.xyz, v0.wxz, v0.wyz
mad r0.xyz, v0.zwy, v0.xzw, -r0.xyz
dp3 r0.x, r0.xyz, v0.xyz
dp3 r1.x, v0.xyz, v0.xyz
dp3 r1.y, v0.yzw, v0.xyz
dp3 r1.z, v0.zwx, v0.xyz
div o0.xyz, r1.xyz, r0.xxx
mov o0.w, r0.x
ret
