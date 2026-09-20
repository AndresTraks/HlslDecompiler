ds_5_0
dcl_input_control_point_count 3
dcl_tessellator_domain domain_tri
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[4], immediateIndexed
dcl_input vDomain.xyz
dcl_input vicp[3][0].xyz
dcl_output_siv o0, position
dcl_temps 1
mul r0.xyz, vDomain.yyy, vicp[1][0].xyz
mad r0.xyz, vicp[0][0].xyz, vDomain.xxx, r0.xyz
mad r0.xyz, vicp[2][0].xyz, vDomain.zzz, r0.xyz
mov r0.w, l(1)
dp4 o0.x, r0, cb0[0]
dp4 o0.y, r0, cb0[1]
dp4 o0.z, r0, cb0[2]
dp4 o0.w, r0, cb0[3]
ret
