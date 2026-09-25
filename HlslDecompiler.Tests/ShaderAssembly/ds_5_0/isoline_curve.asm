ds_5_0
dcl_input_control_point_count 2
dcl_tessellator_domain domain_isoline
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[5], immediateIndexed
dcl_input vDomain.x
dcl_input vicp[2][0].xyz
dcl_input vicp[2][1].xyz
dcl_output_siv o0, position
dcl_temps 2
add r0.xyz, -vicp[0][1].xyz, vicp[1][1].xyz
mad r0.xyz, vDomain.xxx, r0.xyz, vicp[0][1].xyz
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul r0.xyz, r0.www, r0.xyz
mul r0.w, vDomain.x, cb0[4].x
sincos r0.w, null, r0.w
mul r0.xyz, r0.www, r0.xyz
add r1.xyz, -vicp[0][0].xyz, vicp[1][0].xyz
mad r1.xyz, vDomain.xxx, r1.xyz, vicp[0][0].xyz
mad r0.xyz, r0.xyz, cb0[4].yyy, r1.xyz
mov r0.w, l(1)
dp4 o0.x, r0, cb0[0]
dp4 o0.y, r0, cb0[1]
dp4 o0.z, r0, cb0[2]
dp4 o0.w, r0, cb0[3]
ret
