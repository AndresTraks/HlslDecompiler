ds_5_0
dcl_input_control_point_count 3
dcl_tessellator_domain domain_tri
dcl_globalFlags refactoringAllowed | enableMinimumPrecision
dcl_input vpc0.y
dcl_input vDomain.xyz
dcl_input vicp[3][0]
dcl_output_siv o0, position
dcl_temps 1
mul r0, vDomain.y, vicp[1][0]
mad r0, vicp[0][0], vDomain.x, r0
mad r0, vicp[2][0], vDomain.z, r0
mul o0, r0, vpc0.y
ret
