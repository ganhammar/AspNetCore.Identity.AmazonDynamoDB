if [ "pull_request" = "pull_request" ]; then
    if [ "abc-rpg/AspNetCore.Identity.AmazonDynamoDB" != "ganhammar/AspNetCore.Identity.AmazonDynamoDB" ]; then
        echo "is_fork=true"
    else
        echo "is_fork=false"
    fi
else
    echo "is_fork=false"
fi