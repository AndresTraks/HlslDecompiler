ds_5_0
dcl_input_control_point_count 3
dcl_tessellator_domain domain_tri
dcl_globalFlags refactoringAllowed
dcl_input vpc0.y
dcl_input vDomain.xyz
dcl_input vicp[3][0]
dcl_input vicp[3][1].x
dcl_output_siv o0, position
dcl_output o1.x
dcl_temps 1
mul r0, vDomain.y, vicp[1][0]
mad r0, vicp[0][0], vDomain.x, r0
mad o0, vpc0.y, vDomain.z, r0
lt r0.x, l(0.5), vDomain.x
xor r0.y, vpc0.y, vicp[0][1].x
movc o1.x, r0.x, r0.y, vDomain.y
ret
